# AGENTS.md

## Project boundary

`Transiever.Common` contains small shared Transiever libraries.
`Transiever.Common.Accounts` owns non-secret account profiles and platform credential-store access.
`Transiever.Common.Cli` owns stateless hidden-key reading, required single-line input, and secret-option assignment matching.
They are independently consumable and do not reference each other.
The repository does not own protocol transports, Sieve rules, provider SDKs, OAuth acquisition or renewal, login UI, or CLI configuration policy.

The package targets `net10.0`, enables nullable reference types and implicit global usings, and must remain usable without this workspace or a sibling checkout.

## Canonical docs

| Topic | Owner |
| --- | --- |
| Public Accounts API and package usage | `src/Transiever.Common.Accounts/README.md` |
| Public CLI input API and package usage | `src/Transiever.Common.Cli/README.md` |
| Profile and credential lifecycle | `docs/authentication.md` |
| Ownership and profile-to-credential flow | `docs/architecture.md` |
| Deterministic offline validation | `docs/testing.md` |
| Public overview and development commands | `README.md` |

## Validation

```bash
dotnet restore Transiever.Common.slnx
dotnet build Transiever.Common.slnx --configuration Release --no-restore
dotnet test --solution Transiever.Common.slnx --configuration Release --no-build
dotnet pack src/Transiever.Common.Accounts/Transiever.Common.Accounts.csproj --configuration Release --no-build
dotnet pack src/Transiever.Common.Cli/Transiever.Common.Cli.csproj --configuration Release --no-build
```

Tests are deterministic and offline.
No real provider, credential-service account, or sibling checkout is required.

## Security

Profiles contain only non-secret fields.
Never add a plaintext credential fallback or log, serialize, or place credentials in workflow artifacts.
Platform stores remain internal to the package; callers receive redacted lifecycle outcomes only.

## Release

GitHub Actions run repository-local CI.
Releases are manually dispatched from `main` for stable versions or `dev` for beta versions.
NuGet publishing uses GitHub OIDC trusted publishing through `NuGet/login`; do not add a long-lived token.
Both packages use the same version calculated by semantic-release and the same repository release tag.
Before publishing a new package, verify its NuGet trusted-publisher permission for this repository's release workflow; existing package permissions do not establish it.
