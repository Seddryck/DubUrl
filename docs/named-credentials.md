---
title: Named CLI credentials
permalink: /named-credentials/
nav_exclude: true
---

# Named CLI credentials

Save a username/password pair under a portable name, then reference it without
putting the password in a connection URL or command argument:

```text
duburl credential add orders-dev --username alice
duburl connection check --connection development --credential orders-dev
duburl schema deploy schema.json --connection development --credential orders-dev
duburl credential exists orders-dev
duburl credential remove orders-dev
```

`add` asks for a password with input hidden. Escape/Ctrl+C cancels with exit 130.
Without an interactive terminal it fails promptly; choose `--password-stdin` for
automation. There is no `--password <value>` option. `--password-stdin` reads to EOF,
removes exactly one final LF or CRLF, and preserves all other whitespace, embedded
line endings and password characters. An empty password is allowed. Passwords
must fit the portable Windows limit of 1280 UTF-16 code units; stdin reading is
bounded. Usernames must be nonempty, at most 512 UTF-16 code units, without control
characters. Usernames and passwords must be well-formed Unicode.

For PowerShell, a CI-injected password can be piped without including its literal
value in the command (disable command tracing and use the CI platform's masking):

```powershell
$env:DATABASE_PASSWORD | duburl credential add orders-dev --username alice --password-stdin
```

For Bash:

```bash
printf '%s' "$DATABASE_PASSWORD" | duburl credential add orders-dev --username alice --password-stdin
```
The final newline rule still applies to the value supplied through either shell.
To preserve a password that ends with LF or CRLF, append one additional LF as the
transport terminator. Prefer runtime CI secret injection when persistent storage
is unnecessary; the existing `--connection-env` workflow remains available.

Names contain 1–128 ASCII letters, digits, dots, hyphens or underscores, beginning
with a letter or digit. They are case-insensitive and stored in lowercase. Paths,
spaces and Unicode names are rejected. A name identifies exactly one pair; the
username is not an additional lookup key. Never include secrets in names.

`add` refuses an existing name before prompting. `--replace` permits updating both
username and password and also works for a new name. Cooperating DubUrl processes
serialize operations per name to protect create-if-absent behavior. External
credential tools do not participate in that lock. `remove` succeeds if the entry
is already absent.

`exists` writes nothing and checks presence without decoding or loading password
data or contacting a database. A discoverable locked item still exists. WinCred
does not offer a metadata-only query: its native enumeration buffer can contain
secret blobs, but DubUrl inspects only the exact target's name and type and never
decodes the blob during `exists`.

| Outcome | Exit code |
| --- | --- |
| Successful add/remove, or present entry | 0 |
| Absent entry (`exists` only) | 1 |
| Invalid name/input, or no interactive password source | 2 |
| Duplicate without `--replace`, unavailable/locked store, denied access or backend error | 10 |
| Cancelled operation | 130 |

## Native stores

| Platform | Mapping and prerequisites |
| --- | --- |
| Windows | Current-user Credential Manager generic credential, target `duburl/<name>`, stored username and UTF-16 password blob. Local-machine persistence survives logoff for this user on this machine. |
| macOS | Keychain generic password, service `duburl/<name>`, account set to the username and UTF-8 password data. Uses native Security/CoreFoundation APIs and the user's default Keychain behavior. Replacement searches by service so a username change updates the existing entry. |
| Linux | Native `libsecret-1.so.0` and a Secret Service implementation reachable through the session bus, with an existing default collection. Attributes are `application=duburl`, `credential=<name>`; the username/password pair is JSON inside secret data, not lookup attributes. |

Entries persist according to the native store's policy; passwords are never passed
to child processes or written to application configuration files. There is no
plaintext fallback. The commands operate on the current user's store and do not
change machine-wide configuration.

On Linux, unlock the default collection externally before reading, writing or
removing credentials. DubUrl checks the lock state without explicitly requesting an unlock;
metadata-only existence checks can still find locked items. Each native libsecret
operation has a 30-second budget and propagates cancellation. A service may request
native authorization for an operation; libsecret handles such requests within that
budget. Headless servers,
SSH sessions, containers and CI may lack the libraries, session bus, service or
unlocked collection. These produce a store error, never an absent-entry result.

On macOS, CredOS disables native authentication UI for every command. Unlock the
Keychain externally before retrying if access requires it. Native Security/WinCred
calls are synchronous; cancellation is checked
before calls and mutations, but cannot interrupt an already running native call.
A successful write is not rolled back if cancellation arrives after the OS commits it.

## CredOS integration and compatibility

The CLI references the `CredOS` 0.1.0 NuGet package. Only the internal
`CredOsNamedCredentialStore` adapter uses CredOS types. Commands, authentication
sources and hosts depend on DubUrl's `INamedCredentialStore`, which exposes only
DubUrl credential values, cancellation tokens and DubUrl exceptions. The adapter
converts values and maps duplicate failures to `CredentialStoreException` with
`AlreadyExists=true`; other backend failures become a safe store error without an
inner exception. Cancellation remains cancellation. Tests and embedding hosts can
supply their own `INamedCredentialStore` through `CliComposition.CreateDefault`.
The Core, Application and authentication libraries have no CredOS package reference.
The CLI still needs the CredOS runtime assembly; an interface isolates source/API
dependencies, it does not remove the implementation's runtime dependency.

CredOS owns native API calls, per-name locking, persistence and timeouts. It uses
the application scope `duburl`. Existing Windows entries remain addressable because
WinCred targets are case-insensitive, and Linux attributes/JSON retain the same
mapping. On macOS the old service was `DubUrl/<name>`; CredOS uses
`duburl/<name>`. Recreate old named entries with `credential add` before relying on
them, then remove the old service using Keychain Access. No automatic copying or
fallback lookup takes place. Usernames longer than 512 UTF-16 code units or invalid
Unicode values must also be corrected before reuse. Avoid concurrent writes from
old and new CLI versions: they use different mutex namespaces.

CredOS does not expose arbitrary native-target reads. The read-only adapter for
legacy `--credential-store` references remains separate to preserve their existing
mappings; those entries are not implicitly treated as CredOS-managed credentials.

See the [CredOS API contract](https://github.com/Seddryck/CredOS/blob/v0.1.0/docs/api.md).
If NuGet indexing is delayed, download `CredOS.0.1.0.nupkg` from the
[official release](https://github.com/Seddryck/CredOS/releases/tag/v0.1.0) into a
local directory, then restore with both `--source <directory>` and
`--source https://api.nuget.org/v3/index.json`. No repository or machine-wide
NuGet source configuration is needed.

## Existing credential references

`--credential <name>` uses entries managed by these commands. The existing
`--credential-store <reference>` continues reading explicitly provisioned OS entries
using its original mappings; it does not change meaning or try another source on
failure. See [CLI authentication](cli-authentication.md). `--credential`,
`--credential-store` and `--password-prompt` are mutually exclusive. An optional
`--username` must agree with the stored username. Dry runs validate the selection
without accessing the store or prompting.

`duburl capabilities --json` advertises `credentialCommands` and the
`--credential` authentication option. Backend availability still depends on the
current session.

## Validation

Focused command tests use fake stores and prompts. The opt-in
`NativeNamedCredentialStoreTests` create only random fixture-owned entries and
remove them in cleanup. To run on Windows or macOS with an unlocked native store:

```powershell
$env:DUBURL_TEST_NATIVE_CREDENTIALS = '1'
dotnet test DubUrl.Cli.Testing/DubUrl.Cli.Testing.csproj --filter FullyQualifiedName~NativeNamedCredentialStoreTests
```

For Linux, build the test assemblies on the host, then use the disposable image:

```powershell
docker build -t duburl-native-credentials-smoke build/credential-store-smoke
docker run --rm --network none --mount "type=bind,source=$($PWD.Path),target=/workspace,readonly" duburl-native-credentials-smoke
```

The Linux harness compiles a small runner in the container and invokes the same
three native test methods directly, avoiding Windows-generated VSTest manifest
incompatibility. It additionally verifies locked-store and missing-session behavior
in a private GNOME Keyring session. Its entire keyring is destroyed with the
container. It never mounts a host keyring. NReco's connection-only consumer checks
exercise source resolution/mapping without real database I/O; they do not prove a
database login. Actual platform results and unrun coverage belong in the PR report.
