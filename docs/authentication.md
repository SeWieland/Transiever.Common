# Authentication and credential lifecycle

## Profiles

`AccountProfile` contains a display name, provider label, provider account identity, hostname, port, transport security, username, optional preferred-authentication label, and UTC creation/update times.
These are non-secret settings; do not put passwords, tokens, or client secrets into any profile field.
Transport security is `StartTlsRequired`, `ImplicitTls`, or `PlainText`; each consumer decides whether it permits an unencrypted connection.
An authentication label is descriptive and does not implement or select a provider's OAuth flow.

`AccountReference` uses the canonical `account:<guid>` representation in text and in the profile's JSON `reference` field.
The profile-specific converter does not change standalone serialization of the previously published `AccountReference` value type.
Create generates a new reference; update keeps it unchanged, including when the display name, username, or endpoint changes.
Credential keys derive only from that stable reference and credential kind.

`JsonAccountProfileStore` uses `Environment.SpecialFolder.ApplicationData`, below `Transiever/accounts`, with one `<guid>.json` file per profile.
It uses generated JSON metadata so persistence remains compatible with trimming and Native AOT.
Unknown JSON fields, invalid settings, and mismatched account references fail without echoing the file contents.
Missing files return null; corrupted files are errors.

Writes create a private temporary file in the same directory, flush it, and atomically replace the destination.
A per-directory write lock prevents overlapping mutations from silently overwriting one another; a competing writer receives an I/O failure and may retry.
Open readers retain the previous complete profile while new readers see the replacement.
Unix directories use mode `0700` and files use `0600`; Windows directories use a protected ACL granting the current user full control, inherited by new files.
The managed directory, profile files, and lock file must not be symlinks or reparse points.
Platform-selected ancestor paths may contain links, such as macOS system paths or an intentionally redirected application-data directory.

## Remembered credentials

`CredentialKind` supports passwords and OAuth refresh tokens.
Short-lived access tokens are not an Accounts persistence concern.
Provider tools acquire and renew tokens, decide authentication mechanisms, and record outcomes after observing a provider response.

| State | Meaning |
| --- | --- |
| `Missing` | The service confirmed that no matching entry exists. |
| `Available` | A remembered secret is present without a known expiry or rejection. This does not prove provider acceptance. |
| `Expired` | The supplied expiry is at or before the current UTC time, or a caller recorded expiry. |
| `Rejected` | A caller recorded provider rejection. |
| `Unavailable` | Storage cannot safely complete the operation, including missing libraries, locked or inaccessible services, malformed entries, or required approval. |

Only an available `CredentialResult` exposes a secret for authentication.
Status and outcome operations never return secret values.
`ToString()` and System.Text.Json serialization of the public result omit secret material.
Callers must not copy `Secret` into their own diagnostics, serialized objects, or workflow artifacts.
Secrets travel through process memory and native APIs, never subprocess command arguments.
Managed strings cannot be reliably erased, so callers should keep them only as long as authentication needs them.

Set replaces the protected entry and clears previous outcome metadata; an optional new expiry applies to the replacement.
Expiry and observed-outcome metadata live inside the same protected OS entry, not in profile JSON or a sidecar file.
Marking an outcome does not contact a provider.
Remove deletes one credential kind and its outcome metadata and returns `Missing` when absence is confirmed.
Account deletion removes both known kinds before deleting the profile.
If any removal is unavailable, the profile remains so credential ownership is not silently lost; a previously removed kind may already be absent when retrying.

## Platform services

| Platform | Integration | Availability |
| --- | --- | --- |
| Windows | Credential Manager generic credentials through `CredReadW`, `CredWriteW`, and `CredDeleteW`. | Requires an accessible credential set in the current user's logon session. OS entry-size limits apply; oversized writes fail without truncation. |
| macOS | Data-protection Keychain generic passwords through `SecItem` APIs. | Requires a user login context and a host executable with authorized Keychain access entitlements. Authentication UI is disabled; missing entitlements, locked entries, or required approval make storage unavailable. |
| Linux | Freedesktop Secret Service through `libsecret-1.so.0`, GLib, and GIO. | Requires an encrypted libsecret session and an unlocked owning collection; creation requires an unlocked default collection. Plain sessions and prompt requests are unavailable. Collections are not created or unlocked automatically. |

macOS explicitly selects the data-protection Keychain; it does not fall back to the legacy file-based Keychain or change the host process's interaction policy.
Keychain access is determined by the host executable's signing, provisioning, and entitlements, not by this library.
Hosts sharing credentials must authorize the same Keychain access group and place it first in their Keychain access-group entitlement so new entries share that default group.
Accounts does not supply an access-group identifier; reads search the host's authorized groups.
Ordinary unsigned CLI hosts use no-store behavior and one-run input.
See [Apple's Keychain implementation and host requirements](https://developer.apple.com/documentation/technotes/tn3137-on-mac-keychains) when packaging a host that needs remembered credentials.

Linux searches omit the unlock flag.
Reads and mutations require a single unlocked match across collections; ambiguous duplicate entries report `Unavailable` without exposing or changing either entry.
Replacement and outcome updates target the existing item in its owning collection, even if the default collection has changed.
A new entry uses the unlocked default collection only when the search confirms absence.
Creation and deletion inspect the raw D-Bus reply and never perform a returned prompt.
Native-library and service failures become `Unavailable` without exposing native error messages or secret material.
The API is synchronous; libsecret setup, searches, and existing-item updates may block according to the native service.
Raw creation and deletion calls use a 30-second timeout.

Native contracts follow the [Windows credential API](https://learn.microsoft.com/windows/win32/api/wincred/), [Apple Keychain API](https://developer.apple.com/documentation/security/keychain-services), and [libsecret service API](https://gnome.pages.gitlab.gnome.org/libsecret/class.Service.html).

## No-store mode and logout

`CredentialStores.CreateUnavailable()` is explicit no-store mode.
Unsupported platforms and unavailable native services also fail closed.
Profiles remain usable, but credential reads, writes, removals, and outcome operations report `Unavailable`; no plaintext or application-encrypted fallback is created.
Consumers can use safe one-run input through a non-echoed terminal prompt, explicit standard input, or documented environment variables.
Headless consumers must never prompt.
Ordinary command-line secret values remain unsafe and must be rejected by the owning CLI.

Deleting a local credential does not prove provider-side revocation.
Provider tools explain and perform any provider-side logout or revocation.
