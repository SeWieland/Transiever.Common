# Authentication and credential lifecycle

An account profile contains only endpoint, port and security settings, username, provider or account identifier, and preferred authentication data.
Its stable account reference identifies separately stored credentials and never embeds a secret.

Supported credential states are available, missing, expired, rejected, and unavailable.
Credential values may be stored only through Windows Credential Manager, macOS Keychain, or Freedesktop Secret Service when available.
No-store environments retain profiles but require safe one-run input; the package never writes a plaintext fallback.

Deleting a local credential does not prove provider-side revocation.
Provider tools explain and perform any provider-side logout or revocation.
