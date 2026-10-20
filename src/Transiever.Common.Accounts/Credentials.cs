using System.Text.Json.Serialization;

namespace Transiever.Common.Accounts;

/// <summary>The remembered secret's purpose; access tokens are not persisted.</summary>
public enum CredentialKind
{
    /// <summary>A server password.</summary>
    Password,
    /// <summary>A refresh token acquired and renewed by a provider tool.</summary>
    OAuthRefreshToken
}

/// <summary>A redacted local credential state, not provider validation.</summary>
public enum CredentialStatus
{
    /// <summary>No remembered credential exists.</summary>
    Missing,
    /// <summary>A remembered credential is available.</summary>
    Available,
    /// <summary>Expiry is known or was reported by a caller.</summary>
    Expired,
    /// <summary>A caller reported provider rejection.</summary>
    Rejected,
    /// <summary>The credential service cannot be used safely.</summary>
    Unavailable
}

/// <summary>A credential read; only an available result carries secret material. System.Text.Json and ToString omit the secret.</summary>
public sealed class CredentialResult
{
    /// <summary>Creates a result, retaining secret material only for an available credential.</summary>
    public CredentialResult(CredentialStatus status, string? secret = null)
    {
        Status = status;
        Secret = status == CredentialStatus.Available ? secret : null;
    }

    /// <summary>Gets the safe-to-display credential state.</summary>
    public CredentialStatus Status { get; }

    /// <summary>Gets secret material for authentication only. Never log or persist this value.</summary>
    [JsonIgnore]
    public string? Secret { get; }

    /// <inheritdoc />
    public override string ToString() => $"CredentialResult {{ Status = {Status} }}";
}

/// <summary>Accesses remembered secrets without exposing storage-specific types.</summary>
public interface ICredentialStore
{
    /// <summary>Reads secret material only when its state is available.</summary>
    CredentialResult Get(AccountReference reference, CredentialKind kind);
    /// <summary>Sets or replaces a secret and clears previous outcome metadata.</summary>
    CredentialStatus Set(AccountReference reference, CredentialKind kind, string secret, DateTimeOffset? expiresAt = null);
    /// <summary>Deletes one local credential and its metadata; this does not revoke provider access.</summary>
    CredentialStatus Remove(AccountReference reference, CredentialKind kind);
    /// <summary>Gets a redacted local state without validating credentials with a provider.</summary>
    CredentialStatus GetStatus(AccountReference reference, CredentialKind kind);
    /// <summary>Records Available, Expired, or Rejected as a caller-observed outcome.</summary>
    CredentialStatus MarkOutcome(AccountReference reference, CredentialKind kind, CredentialStatus outcome);
}
