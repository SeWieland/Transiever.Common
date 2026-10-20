using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;
using System.Runtime.Versioning;

namespace Transiever.Common.Accounts;

/// <summary>Stores user-only, non-secret JSON profiles with atomic replacement.</summary>
public sealed class JsonAccountProfileStore(string? directory = null, TimeProvider? timeProvider = null) : IAccountProfileStore
{
    private readonly string _directory = Path.GetFullPath(directory ?? DefaultDirectory());
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public AccountProfile Create(AccountProfile profile)
    {
        Validate(profile);
        using var lease = AcquireWriteLock();
        var now = _timeProvider.GetUtcNow();
        var created = profile with { Reference = new AccountReference(Guid.NewGuid()), CreatedAt = now, UpdatedAt = now };
        Write(created, overwrite: false);
        return created;
    }

    /// <inheritdoc />
    public AccountProfile? Get(AccountReference reference)
    {
        var path = ProfilePath(reference);
        if (!Directory.Exists(_directory)) return null;
        RejectLinks(_directory);
        try
        {
            RejectLinks(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var profile = JsonSerializer.Deserialize(stream, AccountsJsonContext.Default.AccountProfile);
            if (profile is null || profile.Reference != reference || profile.CreatedAt == default || profile.UpdatedAt < profile.CreatedAt)
                throw new InvalidDataException("The account profile is invalid.");
            Validate(profile);
            return profile;
        }
        catch (FileNotFoundException) { return null; }
        catch (JsonException) { throw new InvalidDataException("The account profile is invalid."); }
        catch (ArgumentException) { throw new InvalidDataException("The account profile is invalid."); }
    }

    /// <inheritdoc />
    public IReadOnlyList<AccountProfile> List()
    {
        if (!Directory.Exists(_directory)) return [];
        RejectLinks(_directory);
        var profiles = new List<AccountProfile>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "D", out var id)) continue;
            if (Get(new AccountReference(id)) is { } profile) profiles.Add(profile);
        }
        return profiles.OrderBy(profile => profile.DisplayName, StringComparer.Ordinal).ThenBy(profile => profile.Reference.Value).ToArray();
    }

    /// <inheritdoc />
    public AccountProfile Update(AccountProfile profile)
    {
        Validate(profile);
        using var lease = AcquireWriteLock();
        var existing = Get(profile.Reference) ?? throw new KeyNotFoundException("The account profile does not exist.");
        var now = _timeProvider.GetUtcNow();
        var updated = profile with { CreatedAt = existing.CreatedAt, UpdatedAt = now < existing.UpdatedAt ? existing.UpdatedAt : now };
        Write(updated, overwrite: true);
        return updated;
    }

    /// <inheritdoc />
    public void Delete(AccountReference reference)
    {
        var path = ProfilePath(reference);
        using var lease = AcquireWriteLock();
        RejectLinks(path);
        File.Delete(path);
    }

    private FileStream AcquireWriteLock()
    {
        EnsurePrivateDirectory();
        var path = Path.Combine(_directory, ".lock");
        RejectLinks(path);
        return new FileStream(path, PrivateFileOptions(FileMode.OpenOrCreate, FileAccess.ReadWrite));
    }

    private void Write(AccountProfile profile, bool overwrite)
    {
        var path = ProfilePath(profile.Reference);
        var temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, PrivateFileOptions(FileMode.CreateNew, FileAccess.Write)))
            {
                JsonSerializer.Serialize(stream, profile, AccountsJsonContext.Default.AccountProfile);
                stream.Flush(flushToDisk: true);
            }
            RejectLinks(path);
            if (overwrite)
            {
                if (OperatingSystem.IsWindows()) RestrictWindowsFileAccess(path);
                File.Replace(temporary, path, destinationBackupFileName: null);
            }
            else File.Move(temporary, path);
        }
        finally { File.Delete(temporary); }
    }

    [SupportedOSPlatform("windows")]
    private static void RestrictWindowsFileAccess(string path)
    {
        // ReplaceFile preserves the destination DACL, so harden it before replacing the content.
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User ?? throw new UnauthorizedAccessException("The current user could not be determined.");
        var security = new FileSecurity();
        security.SetOwner(owner);
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl, AccessControlType.Allow));
        new FileInfo(path).SetAccessControl(security);
    }

    private static FileStreamOptions PrivateFileOptions(FileMode mode, FileAccess access)
    {
        var options = new FileStreamOptions { Mode = mode, Access = access, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }

    private void EnsurePrivateDirectory()
    {
        RejectLinks(_directory);
        if (OperatingSystem.IsWindows())
        {
            using var identity = WindowsIdentity.GetCurrent();
            var owner = identity.User ?? throw new UnauthorizedAccessException("The current user could not be determined.");
            var security = new DirectorySecurity();
            security.SetOwner(owner);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(owner, FileSystemRights.FullControl,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            var info = new DirectoryInfo(_directory);
            if (!info.Exists) info.Create(security);
            else info.SetAccessControl(security);
        }
        else
        {
            const UnixFileMode permissions = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            Directory.CreateDirectory(_directory, permissions);
            File.SetUnixFileMode(_directory, permissions);
        }
    }

    private string ProfilePath(AccountReference reference)
    {
        if (reference.Value == Guid.Empty) throw new ArgumentException("An account reference must not be empty.", nameof(reference));
        return Path.Combine(_directory, reference.Value.ToString("D") + ".json");
    }

    private static void RejectLinks(string path)
    {
        // Reject redirects of managed entries; OS application-data ancestors may legitimately contain symlinks.
        var attributes = new FileInfo(path).Attributes;
        if (attributes != (FileAttributes)(-1) && (attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The account store cannot use symbolic links or reparse points for managed entries.");
    }

    private static string DefaultDirectory()
    {
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(applicationData)) throw new IOException("The user application-data directory is unavailable.");
        return Path.Combine(applicationData, "Transiever", "accounts");
    }

    private static void Validate(AccountProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        RequireField(profile.DisplayName);
        RequireField(profile.Provider);
        RequireField(profile.AccountId);
        RequireField(profile.Host);
        RequireField(profile.Username);
        if (profile.Port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(profile), "The account port must be between 1 and 65535.");
        if (!Enum.IsDefined(profile.Security)) throw new ArgumentException("The transport-security setting is invalid.", nameof(profile));
    }

    private static void RequireField(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl))
            throw new ArgumentException("Account fields must be nonempty and must not contain control characters.");
    }
}
