# CLI authentication

Schema deployment and connection checking accept `--password-prompt`,
`--credential-store <reference>`, and optional `--username <name>`.
There is deliberately no password command-line option.

```text
duburl schema deploy schema.json --connection development --username alice --password-prompt
duburl connection check --connection development --credential-store development --engine
duburl schema deploy schema.json --connection development --credential-store development --dry-run
duburl connection check --connection-env DATABASE_URL
```

Prompt and store selection conflict; username alone is invalid. The hidden prompt
requires an interactive terminal and a username either in configuration/URL or
`--username`. It never echoes characters; Escape/Ctrl+C cancels with exit 130.
Redirected/unattended calls fail promptly without opening a prompt. Configuration
and URL credential conflicts follow the shared authentication contract. Dry runs
validate combinations and render SQL without credential acquisition or DB opening.

The OS store feature reads **existing** entries only:

| Platform | Backend / reference | Provisioning and prerequisites |
|---|---|---|
| Windows | Credential Manager generic credential target | Create a Generic Credential in Windows Credential Manager with the chosen target, username and password. Uses current-user `CredReadW`; no enumeration or writes. |
| macOS | Keychain generic password service | Create a generic password with the reference as service using Keychain Access. `/usr/bin/security find-generic-password -s <reference> -w` must be usable in the current login session. Supply username separately/in the URL. |
| Linux | Secret Service item with attributes `service=duburl`, `entry=<reference>` | Provision using the desktop keyring UI or `secret-tool store --label=DubUrl service duburl entry <reference>` (password is read from stdin). Requires libsecret `secret-tool`, an unlocked service and usable session bus. Supply username separately/in the URL. |

The helper processes have a 30-second acquisition limit and are terminated/awaited
on cancellation. Backend failures are sanitized; check that the entry exists, the
store is unlocked and its session/backend is available. There is no file fallback.
macOS/Secret Service utilities return one trailing line terminator; exactly that
terminator is removed. Windows credential blobs must contain UTF-16 passwords.

Environment variables remain supported for CI, but are process environment values
and do not have the access/locking semantics of an OS credential store. Prefer
the CI platform's secret injection, avoid logging URLs and never put credentials
in shell arguments. No machine-wide configuration is changed by these adapters.

Provider QA distinguishes controlled adapters from real hidden-terminal and
credential-store smoke fixtures. See [CLI authentication QA](cli-authentication-qa.md)
for implemented cases, fixture prerequisites and unsupported/unverified platforms.
Native-store tests use only uniquely named fixture-owned entries and remove them;
they do not enumerate or read pre-existing user entries. Platform availability
does not imply that a skipped native smoke test passed.

## OAuth CLI sign-in

Explicit browser and device-code selection is documented in [OAuth sign-in from the CLI](cli-oauth.md), including supported providers, unattended/JSON behavior and exact live-validation gaps. Password selection remains available and is exclusive with OAuth.
