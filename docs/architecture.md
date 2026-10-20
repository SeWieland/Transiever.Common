# Architecture

`Transiever.Common.Accounts` is the shared boundary between Transiever tool configuration and the operating system credential service:

```text
tool CLI -> account profile -> Accounts package -> platform credential service
```

The package owns account references, non-secret profile persistence, credential lifecycle status, and redaction.
Tools own command presentation, provider authentication, token renewal, and provider-side revocation.
ManageSieve and the provider-neutral SieveRuler library remain storage-free.

`IAccountProfileStore` and `ICredentialStore` are the public injection boundaries for callers and offline tests.
`JsonAccountProfileStore` implements non-secret persistence independently of credential-service availability.
`CredentialStores` selects an internal Windows, macOS, or Linux adapter; callers never receive native storage types.
The package uses runtime libraries and native platform APIs without a CLI parser, provider SDK, protocol dependency, or dependency-injection framework.

A credential is scoped by the account reference and `CredentialKind`, not by editable connection settings.
The credential lifecycle layer stores expiry and observed-outcome metadata together with the secret inside the native protected entry.
The profile serializer cannot see those credential records.
Only available reads return secret material, and the public read result excludes it from diagnostics and serialization.

`AccountManager` coordinates account deletion by removing both known credential kinds before the profile.
Native failures preserve the profile and surface an unavailable result, allowing cleanup to be retried.
There is no transaction across independent native entries: a failed deletion may have already removed an earlier credential kind.
Native adapters never substitute filesystem credential storage or initiate provider operations.

Platform choices and lifecycle behavior are documented in the [authentication guide](authentication.md).
The focused public API and usage examples belong in the [package guide](../src/Transiever.Common.Accounts/README.md).
