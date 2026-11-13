# Transiever.Common

`Transiever.Common` contains focused .NET 10 libraries shared by Transiever tools.
`Transiever.Common.Accounts` owns non-secret account profiles and safe access to platform credential stores.
`Transiever.Common.Cli` provides stateless secret-input mechanics while tools retain their configuration and authentication policy.

## Install

```bash
dotnet add package Transiever.Common.Accounts
```

`Transiever.Common.Cli` is currently an unpublished candidate.
Use an explicit local package source for [candidate validation](#development).
After publication, install it from NuGet.org:

```bash
dotnet add package Transiever.Common.Cli
```

Install either package independently; neither requires the other.
They do not implement provider login, OAuth acquisition or renewal, network protocols, Sieve rules, or a shared CLI framework.

## Documentation Map

* [Accounts package guide](src/Transiever.Common.Accounts/README.md)
* [CLI input package guide](src/Transiever.Common.Cli/README.md)
* [authentication and credential lifecycle](docs/authentication.md)
* [architecture](docs/architecture.md)
* [testing](docs/testing.md)

## Development

```bash
dotnet restore Transiever.Common.slnx
dotnet build Transiever.Common.slnx --configuration Release --no-restore
dotnet test --solution Transiever.Common.slnx --configuration Release --no-build
dotnet pack src/Transiever.Common.Accounts/Transiever.Common.Accounts.csproj --configuration Release --no-build
dotnet pack src/Transiever.Common.Cli/Transiever.Common.Cli.csproj --configuration Release --no-build
```

For candidate validation, restore a separate consumer from an explicit local source containing the packed Cli artifact and pin its packed version.
Local-feed validation does not establish public availability.
See the [testing guide](docs/testing.md) for coverage and validation limits.

## Publication

Stable releases come from `main`; beta prereleases come from `dev`.
The GitHub Actions release workflow is configured to publish packages to NuGet.org using trusted publishing.
Accounts and Cli share the version calculated for each repository release.

## AI usage

Transiever is a personal hobby project created to solve practical problems I have encountered myself.

AI is used heavily throughout its development.
It supports research, design exploration, implementation, debugging, and documentation.

The project is developed using test-driven development and reviewed by a human, me.
Its direction, behavior, and quality remain guided by the problems it is intended to solve.
