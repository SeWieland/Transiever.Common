using System.Text.Json;

namespace Transiever.Common.Accounts.UnitTest;

public sealed class CredentialLifecycleTests
{
    private readonly AccountReference _reference = AccountReference.Parse("account:6d53f44e-6c16-429c-a1e8-d8ef332f2232");

    [Fact]
    public void Remembered_credentials_are_scoped_by_account_and_kind_and_can_be_removed()
    {
        var native = new MemoryPlatform();
        var store = new PlatformCredentialStore(native);
        Assert.Equal(CredentialStatus.Missing, store.GetStatus(_reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Available, store.Set(_reference, CredentialKind.Password, "private-password"));
        store.Set(_reference, CredentialKind.OAuthRefreshToken, "private-refresh-token");
        Assert.Equal("private-password", store.Get(_reference, CredentialKind.Password).Secret);
        Assert.Equal("private-refresh-token", store.Get(_reference, CredentialKind.OAuthRefreshToken).Secret);
        Assert.Equal(CredentialStatus.Missing, store.GetStatus(new AccountReference(Guid.NewGuid()), CredentialKind.Password));
        Assert.Equal(CredentialStatus.Missing, store.Remove(_reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Missing, store.GetStatus(_reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Available, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
    }

    [Fact]
    public void Expiry_boundary_and_observed_outcomes_hide_secrets_and_replacement_clears_metadata()
    {
        var clock = new Clock();
        var native = new MemoryPlatform();
        var store = new PlatformCredentialStore(native, clock);
        var expires = clock.GetUtcNow().AddMinutes(1);
        store.Set(_reference, CredentialKind.OAuthRefreshToken, "old-token", expires);
        Assert.Equal(CredentialStatus.Available, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
        clock.Now = expires;
        Assert.Equal(CredentialStatus.Expired, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
        Assert.Null(store.Get(_reference, CredentialKind.OAuthRefreshToken).Secret);
        Assert.Equal(CredentialStatus.Rejected, store.MarkOutcome(_reference, CredentialKind.OAuthRefreshToken, CredentialStatus.Rejected));
        Assert.Equal(CredentialStatus.Rejected, new PlatformCredentialStore(native, clock).GetStatus(_reference, CredentialKind.OAuthRefreshToken));
        Assert.Null(store.Get(_reference, CredentialKind.OAuthRefreshToken).Secret);
        store.Set(_reference, CredentialKind.OAuthRefreshToken, "new-token");
        Assert.Equal(CredentialStatus.Available, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
        Assert.Equal("new-token", store.Get(_reference, CredentialKind.OAuthRefreshToken).Secret);
        store.MarkOutcome(_reference, CredentialKind.OAuthRefreshToken, CredentialStatus.Expired);
        Assert.Equal(CredentialStatus.Expired, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
        store.Remove(_reference, CredentialKind.OAuthRefreshToken);
        Assert.Equal(CredentialStatus.Missing, store.GetStatus(_reference, CredentialKind.OAuthRefreshToken));
    }

    [Fact]
    public void No_store_is_explicit_and_never_returns_a_secret()
    {
        var store = CredentialStores.CreateUnavailable();
        Assert.Equal(CredentialStatus.Unavailable, store.Set(_reference, CredentialKind.Password, "private-password"));
        Assert.Equal(CredentialStatus.Unavailable, store.Get(_reference, CredentialKind.Password).Status);
        Assert.Null(store.Get(_reference, CredentialKind.Password).Secret);
        Assert.Equal(CredentialStatus.Unavailable, store.Remove(_reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Unavailable, store.MarkOutcome(_reference, CredentialKind.Password, CredentialStatus.Rejected));
    }

    [Fact]
    public void Credential_results_are_redacted_in_diagnostics_and_serialization()
    {
        var result = new CredentialResult(CredentialStatus.Available, "private-password");
        Assert.DoesNotContain("private-password", result.ToString());
        Assert.DoesNotContain("private-password", JsonSerializer.Serialize(result));
        Assert.Contains("Available", result.ToString());
        Assert.Null(new CredentialResult(CredentialStatus.Rejected, "private-password").Secret);
    }

    [Fact]
    public void Native_failures_and_invalid_payloads_report_unavailable_without_error_content()
    {
        var native = new MemoryPlatform { Error = true };
        var store = new PlatformCredentialStore(native);
        Assert.Equal(CredentialStatus.Unavailable, store.GetStatus(_reference, CredentialKind.Password));
        Assert.Equal(CredentialStatus.Unavailable, store.Set(_reference, CredentialKind.Password, "private-password"));
        Assert.Equal(CredentialStatus.Unavailable, store.Remove(_reference, CredentialKind.Password));
        native.Error = false;
        native.Malformed = true;
        Assert.Equal(CredentialStatus.Unavailable, store.GetStatus(_reference, CredentialKind.Password));
    }

    [Fact]
    public void Invalid_credential_inputs_do_not_reach_native_storage()
    {
        var store = new PlatformCredentialStore(new MemoryPlatform());
        Assert.Throws<ArgumentException>(() => store.Set(default, CredentialKind.Password, "private-password"));
        Assert.Throws<ArgumentException>(() => store.Set(_reference, CredentialKind.Password, "bad\0secret"));
        Assert.Throws<ArgumentException>(() => store.Set(_reference, CredentialKind.Password, "bad\uD800secret"));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.Get(_reference, (CredentialKind)42));
        Assert.Throws<ArgumentOutOfRangeException>(() => store.MarkOutcome(_reference, CredentialKind.Password, CredentialStatus.Missing));
    }

    internal sealed class MemoryPlatform : IPlatformSecretStore
    {
        private readonly Dictionary<string, string> _values = [];
        public bool Error { get; set; }
        public bool Malformed { get; set; }
        public CredentialResult Read(string key) => Error ? new(CredentialStatus.Unavailable)
            : Malformed ? new(CredentialStatus.Available, "not-json-private-secret")
            : _values.TryGetValue(key, out var value) ? new(CredentialStatus.Available, value) : new(CredentialStatus.Missing);
        public CredentialStatus Write(string key, string value)
        {
            if (Error) return CredentialStatus.Unavailable;
            _values[key] = value;
            return CredentialStatus.Available;
        }
        public CredentialStatus Delete(string key)
        {
            if (Error) return CredentialStatus.Unavailable;
            _values.Remove(key);
            return CredentialStatus.Missing;
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
