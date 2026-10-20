using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

[assembly: InternalsVisibleTo("Transiever.Common.Accounts.UnitTest")]

namespace Transiever.Common.Accounts;

internal interface IPlatformSecretStore
{
    CredentialResult Read(string key);
    CredentialStatus Write(string key, string value);
    CredentialStatus Delete(string key);
}

internal sealed class PlatformCredentialStore(IPlatformSecretStore platform, TimeProvider? timeProvider = null) : ICredentialStore
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public CredentialResult Get(AccountReference reference, CredentialKind kind)
    {
        var (status, entry) = ReadEntry(Key(reference, kind));
        if (entry is null) return new CredentialResult(status);
        status = EntryStatus(entry);
        return new CredentialResult(status, status == CredentialStatus.Available ? entry.Secret : null);
    }

    public CredentialStatus Set(AccountReference reference, CredentialKind kind, string secret, DateTimeOffset? expiresAt = null)
    {
        var key = Key(reference, kind);
        if (string.IsNullOrEmpty(secret) || secret.Contains('\0'))
            throw new ArgumentException("A credential must be nonempty and must not contain a null character.", nameof(secret));
        try { _ = new UTF8Encoding(false, true).GetByteCount(secret); }
        catch (EncoderFallbackException) { throw new ArgumentException("A credential must contain valid Unicode.", nameof(secret)); }
        var entry = new CredentialEntry { Secret = secret, ExpiresAt = expiresAt };
        var status = platform.Write(key, JsonSerializer.Serialize(entry, CredentialJsonContext.Default.CredentialEntry));
        return status == CredentialStatus.Available ? EntryStatus(entry) : CredentialStatus.Unavailable;
    }

    public CredentialStatus Remove(AccountReference reference, CredentialKind kind) => platform.Delete(Key(reference, kind));
    public CredentialStatus GetStatus(AccountReference reference, CredentialKind kind) => Get(reference, kind).Status;

    public CredentialStatus MarkOutcome(AccountReference reference, CredentialKind kind, CredentialStatus outcome)
    {
        var key = Key(reference, kind);
        if (outcome is not (CredentialStatus.Available or CredentialStatus.Expired or CredentialStatus.Rejected))
            throw new ArgumentOutOfRangeException(nameof(outcome), "Only observed Available, Expired, or Rejected outcomes can be recorded.");
        var (status, entry) = ReadEntry(key);
        if (entry is null) return status;
        entry.Outcome = outcome;
        return platform.Write(key, JsonSerializer.Serialize(entry, CredentialJsonContext.Default.CredentialEntry)) == CredentialStatus.Available
            ? EntryStatus(entry) : CredentialStatus.Unavailable;
    }

    private (CredentialStatus Status, CredentialEntry? Entry) ReadEntry(string key)
    {
        var result = platform.Read(key);
        if (result.Status != CredentialStatus.Available) return (result.Status, null);
        if (result.Secret is null) return (CredentialStatus.Unavailable, null);
        try
        {
            var entry = JsonSerializer.Deserialize(result.Secret, CredentialJsonContext.Default.CredentialEntry);
            if (entry is null || string.IsNullOrEmpty(entry.Secret) || entry.Secret.Contains('\0')
                || entry.Outcome is not (CredentialStatus.Available or CredentialStatus.Expired or CredentialStatus.Rejected))
                return (CredentialStatus.Unavailable, null);
            return (CredentialStatus.Available, entry);
        }
        catch (JsonException) { return (CredentialStatus.Unavailable, null); }
    }

    private CredentialStatus EntryStatus(CredentialEntry entry)
    {
        if (entry.Outcome is CredentialStatus.Rejected or CredentialStatus.Expired) return entry.Outcome;
        return entry.ExpiresAt <= _timeProvider.GetUtcNow() ? CredentialStatus.Expired : CredentialStatus.Available;
    }

    private static string Key(AccountReference reference, CredentialKind kind)
    {
        if (reference.Value == Guid.Empty) throw new ArgumentException("An account reference must not be empty.", nameof(reference));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        return $"Transiever/accounts/{reference}/{kind}";
    }
}

internal sealed class CredentialEntry
{
    public required string Secret { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public CredentialStatus Outcome { get; set; } = CredentialStatus.Available;
}

[JsonSourceGenerationOptions(UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CredentialEntry))]
internal partial class CredentialJsonContext : JsonSerializerContext;
