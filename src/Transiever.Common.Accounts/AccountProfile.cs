using System.Text.Json.Serialization;

namespace Transiever.Common.Accounts;

/// <summary>Non-secret connection and identity settings for one account.</summary>
public sealed record AccountProfile
{
    /// <summary>Gets the stable reference assigned by the profile store.</summary>
    [JsonConverter(typeof(AccountReferenceJsonConverter))]
    public AccountReference Reference { get; init; }
    /// <summary>Gets the human-readable account name.</summary>
    public required string DisplayName { get; init; }
    /// <summary>Gets the provider label, without implementing provider policy.</summary>
    public required string Provider { get; init; }
    /// <summary>Gets the provider's non-secret account identity.</summary>
    public required string AccountId { get; init; }
    /// <summary>Gets the server hostname or IP address.</summary>
    public required string Host { get; init; }
    /// <summary>Gets the server port.</summary>
    public int Port { get; init; } = 4190;
    /// <summary>Gets the transport-security setting.</summary>
    public TransportSecurity Security { get; init; } = TransportSecurity.StartTlsRequired;
    /// <summary>Gets the authentication username.</summary>
    public required string Username { get; init; }
    /// <summary>Gets an optional authentication preference label.</summary>
    public string? PreferredAuthentication { get; init; }
    /// <summary>Gets the UTC creation time assigned by the store.</summary>
    public DateTimeOffset CreatedAt { get; init; }
    /// <summary>Gets the UTC last-update time assigned by the store.</summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>Transport settings; consumers enforce their own security policy.</summary>
public enum TransportSecurity
{
    /// <summary>Require STARTTLS before authentication.</summary>
    StartTlsRequired,
    /// <summary>Negotiate TLS when connecting.</summary>
    ImplicitTls,
    /// <summary>Use an unencrypted connection, only if a consumer explicitly permits it.</summary>
    PlainText
}
