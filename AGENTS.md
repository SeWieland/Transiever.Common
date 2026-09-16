# AGENTS.md

## Project boundary

`Transiever.Common` contains small shared Transiever libraries.
The initial `Transiever.Common.Accounts` package owns non-secret account profiles and platform credential-store access.
It does not own protocol transports, Sieve rules, provider SDKs, OAuth acquisition or renewal, login UI, or a shared CLI layer.

The package targets `net10.0`, enables nullable reference types and implicit global usings, and must remain usable without this workspace or a sibling checkout.

## Canonical docs

| Topic | Owner |
| --- | --- |
| Public Accounts API and package usage | `src/Transiever.Common.Accounts/README.md` |
| Profile and credential lifecycle | `docs/authentication.md` |
| Ownership and profile-to-credential flow | `docs/architecture.md` |
| Deterministic offline validation | `docs/testing.md` |
| Public overview and development commands | `README.md` |

## Validation

```bash
dotnet restore Transiever.Common.slnx
dotnet build Transiever.Common.slnx --configuration Release --no-restore
dotnet test Transiever.Common.slnx --configuration Release --no-build
dotnet pack src/Transiever.Common.Accounts/Transiever.Common.Accounts.csproj --configuration Release --no-build
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
