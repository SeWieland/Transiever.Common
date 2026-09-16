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
