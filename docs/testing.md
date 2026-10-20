# Testing

The normal test path is deterministic and offline.
It uses synthetic profile data and credential-store seams; it never requires a real provider, OAuth flow, desktop credential profile, or sibling checkout.

```bash
dotnet restore Transiever.Common.slnx
dotnet build Transiever.Common.slnx --configuration Release --no-restore
dotnet test Transiever.Common.slnx --configuration Release --no-build
dotnet pack src/Transiever.Common.Accounts/Transiever.Common.Accounts.csproj --configuration Release --no-build
```

Before publication, restore the packed artifact from an explicit local package source.
The consumer must not require a sibling project reference.

## Offline coverage

Profile tests use an isolated temporary directory with synthetic account data.
They cover persistence, stable identities, timestamps, missing and corrupt JSON, deletion, private access, and atomic replacement.
The atomic-replacement test holds an old file open while updating, then checks that the old reader sees the original complete document and a fresh read sees the replacement.
A Windows sharing failure also verifies that a failed replacement retains the old profile and cleans temporary files.
Unix permission and symlink tests run only on Unix; Windows ACL and sharing tests run only on Windows.
The runner reports unsupported platform cases as skipped.

Credential tests fake the internal native boundary and never call `CredentialStores.CreateDefault()` against a real store.
They cover account/kind scoping, replacement and removal, exact expiry, observed outcomes, metadata clearing, redaction, no-store behavior, native failures, and account-deletion preservation.
Native error mapping is tested with Windows, macOS, and Linux not-found codes, other failures, and absent libraries.
Secret Service prompt replies are tested as failures without invoking a prompt.
Linux session fakes also cover replacement and outcome updates after changing the default collection, preserving the original item's ownership.
Duplicate and locked matches must remain unavailable without reading, replacing, or deleting ambiguous entries.
These tests verify the managed boundary and lifecycle; they do not establish that every native ABI or desktop configuration works.

## Optional native checks

Actual Windows Credential Manager, macOS Keychain, and Linux Secret Service checks require an explicitly authorized, disposable test identity and isolated entries.
They are optional manual checks, not part of normal restore, build, test, or CI.
Never use an existing user's stored credential or print a native credential response.
Provider login and token refresh are outside this package's test scope.

Source-generated JSON metadata and the package's `IsAotCompatible` setting enable trimming/AOT analysis in ordinary builds.
This is distinct from publishing and running a consumer with Native AOT on each target platform.

Publication uses the repository's trusted-publishing workflow.
Local packing and package-mode consumer validation do not publish a package or validate that external release path.
