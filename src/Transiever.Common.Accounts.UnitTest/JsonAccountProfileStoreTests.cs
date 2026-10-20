using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Transiever.Common.Accounts.UnitTest;

public sealed class JsonAccountProfileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "transiever-accounts-tests-" + Guid.NewGuid().ToString("N"));
    private readonly Clock _clock = new();

    [Fact]
    public void Create_and_update_preserve_identity_and_persist_only_profile_fields()
    {
        var store = new JsonAccountProfileStore(_directory, _clock);
        var created = store.Create(Profile());
        Assert.NotEqual(Guid.Empty, created.Reference.Value);
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 12, 0, 0, TimeSpan.Zero), created.CreatedAt);
        _clock.Advance();
        var updated = store.Update(created with { DisplayName = "Renamed", Host = "new.example.test", CreatedAt = DateTimeOffset.MinValue });

        var reloaded = new JsonAccountProfileStore(_directory).Get(created.Reference);
        Assert.Equal(updated, reloaded);
        Assert.Equal(created.Reference, updated.Reference);
        Assert.Equal(created.CreatedAt, updated.CreatedAt);
        Assert.Equal(created.UpdatedAt.AddMinutes(1), updated.UpdatedAt);
        Assert.Equal("new.example.test", reloaded!.Host);
        Assert.Equal("Renamed", Assert.Single(store.List()).DisplayName);
        using var json = JsonDocument.Parse(File.ReadAllText(Assert.Single(Directory.GetFiles(_directory, "*.json"))));
        Assert.Equal(11, json.RootElement.EnumerateObject().Count());
        Assert.Equal(created.Reference.ToString(), json.RootElement.GetProperty("reference").GetString());
        Assert.DoesNotContain(Directory.GetFiles(_directory), file => file.EndsWith(".tmp", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_update_leaves_the_previous_profile_intact()
    {
        var store = new JsonAccountProfileStore(_directory);
        var created = store.Create(Profile());
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Update(created with { Port = 65536 }));
        Assert.Equal(created, store.Get(created.Reference));
    }

    [Fact]
    public void Atomic_replacement_keeps_open_readers_on_the_previous_complete_profile()
    {
        var store = new JsonAccountProfileStore(_directory);
        var created = store.Create(Profile());
        using var oldFile = new FileStream(Assert.Single(Directory.GetFiles(_directory, "*.json")), FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        store.Update(created with { DisplayName = "Replacement" });
        using var oldJson = JsonDocument.Parse(oldFile);
        Assert.Equal("Example", oldJson.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("Replacement", store.Get(created.Reference)!.DisplayName);
    }

    [Fact(SkipUnless = nameof(IsUnix), Skip = "Creating symbolic links in this fixture requires Unix.")]
    public void Managed_directory_symlinks_are_rejected_but_platform_path_ancestors_can_be_links()
    {
        Directory.CreateDirectory(_directory);
        var real = Path.Combine(_directory, "real");
        Directory.CreateDirectory(real);
        var link = Path.Combine(_directory, "link");
        Directory.CreateSymbolicLink(link, real);
        Assert.Throws<IOException>(() => new JsonAccountProfileStore(link).Create(Profile()));
        var store = new JsonAccountProfileStore(Path.Combine(link, "accounts"));
        var created = store.Create(Profile());
        Assert.Equal(created, store.Get(created.Reference));
        Assert.Single(Directory.GetFiles(Path.Combine(real, "accounts"), "*.json"));
    }

    [Fact]
    public void Missing_profiles_are_distinct_from_corrupt_profiles()
    {
        var store = new JsonAccountProfileStore(_directory);
        Assert.Null(store.Get(new AccountReference(Guid.NewGuid())));
        Assert.Empty(store.List());
        var created = store.Create(Profile());
        File.WriteAllText(Assert.Single(Directory.GetFiles(_directory, "*.json")), "{not-json}");
        var error = Assert.Throws<InvalidDataException>(() => store.Get(created.Reference));
        Assert.DoesNotContain("not-json", error.ToString());
    }

    [Fact]
    public void Delete_removes_only_the_selected_profile()
    {
        var store = new JsonAccountProfileStore(_directory);
        var first = store.Create(Profile());
        var second = store.Create(Profile() with { DisplayName = "Second" });
        store.Delete(first.Reference);
        store.Delete(first.Reference);
        Assert.Null(store.Get(first.Reference));
        Assert.Equal(second, Assert.Single(store.List()));
    }

    public static bool IsUnix => !OperatingSystem.IsWindows();
    public static bool IsWindows => OperatingSystem.IsWindows();

    [Fact(SkipUnless = nameof(IsUnix), Skip = "Unix file permissions require a Unix host.")]
    public void Unix_profile_permissions_exclude_other_users()
    {
        if (OperatingSystem.IsWindows()) throw new InvalidOperationException();
        var store = new JsonAccountProfileStore(_directory);
        store.Create(Profile());
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(_directory));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Assert.Single(Directory.GetFiles(_directory, "*.json"))));
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows ACL checks require Windows.")]
    public void Windows_profile_directory_access_is_limited_to_the_current_user()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException();
        new JsonAccountProfileStore(_directory).Create(Profile());
        using var identity = WindowsIdentity.GetCurrent();
        var security = new DirectoryInfo(_directory).GetAccessControl();
        Assert.True(security.AreAccessRulesProtected);
        Assert.Equal(identity.User, security.GetOwner(typeof(SecurityIdentifier)));
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            Assert.Equal(identity.User, rule.IdentityReference);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "Windows ACL checks require Windows.")]
    public void Replacement_restores_user_only_access_when_existing_file_permissions_are_permissive()
    {
        if (!OperatingSystem.IsWindows()) throw new InvalidOperationException();
        var store = new JsonAccountProfileStore(_directory);
        var created = store.Create(Profile());
        var file = new FileInfo(Assert.Single(Directory.GetFiles(_directory, "*.json")));
        using var identity = WindowsIdentity.GetCurrent();
        var security = new FileSecurity();
        security.SetOwner(identity.User!);
        security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(identity.User!, FileSystemRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.Read, AccessControlType.Allow));
        file.SetAccessControl(security);

        store.Update(created with { DisplayName = "Private replacement" });

        foreach (FileSystemAccessRule rule in file.GetAccessControl().GetAccessRules(true, true, typeof(SecurityIdentifier)))
            Assert.True(identity.User == rule.IdentityReference);
        Assert.Equal("Private replacement", store.Get(created.Reference)!.DisplayName);
    }

    [Fact(SkipUnless = nameof(IsWindows), Skip = "This atomic replacement fault uses Windows file-sharing semantics.")]
    public void Failed_atomic_replacement_leaves_previous_profile_and_removes_temporary_file()
    {
        var store = new JsonAccountProfileStore(_directory);
        var created = store.Create(Profile());
        using (var lease = new FileStream(Assert.Single(Directory.GetFiles(_directory, "*.json")), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            var error = Record.Exception(() => store.Update(created with { DisplayName = "Must not replace" }));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(created, store.Get(created.Reference));
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    [Fact]
    public void Stored_profile_references_and_unknown_secret_fields_are_validated_without_disclosing_data()
    {
        var store = new JsonAccountProfileStore(_directory);
        var created = store.Create(Profile());
        var path = Assert.Single(Directory.GetFiles(_directory, "*.json"));
        var original = File.ReadAllText(path);
        File.WriteAllText(path, original.Replace(created.Reference.ToString(), new AccountReference(Guid.NewGuid()).ToString(), StringComparison.Ordinal));
        Assert.Throws<InvalidDataException>(() => store.Get(created.Reference));
        File.WriteAllText(path, original[..^1] + ",\"secret\":\"private-secret\"}");
        var error = Assert.Throws<InvalidDataException>(() => store.Get(created.Reference));
        Assert.DoesNotContain("private-secret", error.ToString());
    }

    [Fact]
    public void Backward_clock_adjustment_does_not_make_updated_profiles_unreadable()
    {
        var store = new JsonAccountProfileStore(_directory, _clock);
        var created = store.Create(Profile());
        _clock.Backward();
        store.Update(created with { DisplayName = "Updated" });
        Assert.Equal("Updated", store.Get(created.Reference)!.DisplayName);
    }

    internal static AccountProfile Profile() => new()
    {
        DisplayName = "Example",
        Provider = "generic",
        AccountId = "mail@example.test",
        Host = "sieve.example.test",
        Username = "mail@example.test",
        PreferredAuthentication = "password"
    };

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddMinutes(1);
        public void Backward() => _now = _now.AddMinutes(-1);
    }
}
