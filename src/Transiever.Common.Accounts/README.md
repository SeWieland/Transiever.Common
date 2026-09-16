# Transiever.Common.Accounts

`Transiever.Common.Accounts` provides the shared account-profile and credential-store boundary for Transiever tools.
Profiles contain non-secret connection and identity data.
Credential values remain in a supported platform credential service or are supplied for one run.

The package does not acquire or renew OAuth tokens, connect to providers, or expose a shared CLI.
See the [authentication guide](https://github.com/SeWieland/Transiever.Common/blob/main/docs/authentication.md) for lifecycle and security rules.
