# Transiever.Common

`Transiever.Common` contains focused .NET 10 libraries shared by Transiever tools.
The initial package, `Transiever.Common.Accounts`, owns non-secret account profiles and safe access to platform credential stores.

## Install

```bash
dotnet add package Transiever.Common.Accounts
```

The package does not implement provider login, OAuth acquisition or renewal, network protocols, Sieve rules, or a shared CLI framework.

## Documentation Map

* [Accounts package guide](src/Transiever.Common.Accounts/README.md)
* [authentication and credential lifecycle](docs/authentication.md)
* [architecture](docs/architecture.md)
* [testing](docs/testing.md)

## Development

```bash
dotnet restore Transiever.Common.slnx
dotnet build Transiever.Common.slnx --configuration Release --no-restore
dotnet test Transiever.Common.slnx --configuration Release --no-build
dotnet pack src/Transiever.Common.Accounts/Transiever.Common.Accounts.csproj --configuration Release --no-build
```

## Publication

Stable releases come from `main`; beta prereleases come from `dev`.
GitHub Actions publish packages to NuGet.org using trusted publishing.

## AI usage

Transiever is a personal hobby project created to solve practical problems I have encountered myself.

AI is used heavily throughout its development.
It supports research, design exploration, implementation, debugging, and documentation.

The project is developed using test-driven development and reviewed by a human, me.
Its direction, behavior, and quality remain guided by the problems it is intended to solve.
