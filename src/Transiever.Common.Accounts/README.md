# Transiever.Common.Accounts

`Transiever.Common.Accounts` provides the shared account-profile and credential-store boundary for Transiever tools.
Profiles contain non-secret connection and identity data.
Credential values remain in a supported platform credential service or are supplied for one run.

The package does not acquire or renew OAuth tokens, connect to providers, or expose a shared CLI.
See the [authentication guide](https://github.com/SeWieland/Transiever.Common/blob/main/docs/authentication.md) for lifecycle and security rules.

## Profiles

```csharp
using Transiever.Common.Accounts;

IAccountProfileStore profiles = new JsonAccountProfileStore();
var profile = profiles.Create(new AccountProfile
{
    DisplayName = "Work mailbox",
    Provider = "generic",
    AccountId = "mail@example.test",
    Host = "sieve.example.test",
    Username = "mail@example.test",
    Port = 4190,
    Security = TransportSecurity.StartTlsRequired,
    PreferredAuthentication = "PLAIN"
});

Console.WriteLine(profile.Reference); // account:<guid>
profile = profiles.Update(profile with { DisplayName = "Work" });
```

`IAccountProfileStore` supplies `Create`, `Get`, `List`, `Update`, and `Delete`.
Create assigns a new reference and UTC timestamps.
Update preserves the reference and creation time.
`JsonAccountProfileStore` accepts an optional directory and `TimeProvider` for isolated hosts and tests.
The default location is the current user's application-data directory below `Transiever/accounts`.
Profile failures throw exceptions without including serialized profile contents.

## Credentials

```csharp
ICredentialStore credentials = CredentialStores.CreateDefault();
CredentialStatus status = credentials.GetStatus(profile.Reference, CredentialKind.Password);
Console.WriteLine(status);
```

`ICredentialStore.Set` sets or replaces a `Password` or `OAuthRefreshToken`, with optional expiry.
Its secret argument must come from an approved safe input source; ordinary command-line secret values are unsafe.
`Get` returns a `CredentialResult`; only `Available` results carry a `Secret` for authentication.
The result's `ToString()` and System.Text.Json representation omit that secret.
`GetStatus`, `Remove`, and `MarkOutcome` report redacted local state.
`MarkOutcome` accepts the caller-observed states `Available`, `Expired`, or `Rejected`.
Replacement clears old expiry and outcome metadata unless a new expiry is supplied.

`CredentialStores.CreateUnavailable()` provides explicit no-store mode for hosts using one-run credentials.
Missing native libraries, inaccessible or locked services, and approval requirements return `Unavailable`.
No credential operation opens an unlock or approval dialog, writes a fallback credential file, or contacts a provider.
The synchronous native calls belong off a graphical application's UI thread.

Use `new AccountManager(profiles, credentials).Delete(reference)` for complete local account removal.
It returns `Missing` after removing both credential kinds and then the profile.
It returns `Unavailable` and preserves the profile if a credential removal cannot be confirmed.
The profile store's own `Delete` removes only non-secret settings and does not coordinate credential cleanup.
Local removal does not revoke credentials at their provider.

Consumers can inject `IAccountProfileStore` and `ICredentialStore` directly; no dependency-injection framework is required.
See [platform behavior and security](https://github.com/SeWieland/Transiever.Common/blob/main/docs/authentication.md) and [offline testing](https://github.com/SeWieland/Transiever.Common/blob/main/docs/testing.md).
