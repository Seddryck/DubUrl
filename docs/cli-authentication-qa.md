# CLI password and credential-store QA (#1690)

Every existing provider QA fixture, including the Oracle and license-free SingleStore suites added upstream in #1709, inherits the same 31 `CliPasswordAuthentication` cases. Existing complete QA runs discover them without workflow or runner changes. Results distinguish controlled adapters from real terminal/store smoke tests; a fake adapter pass never establishes native hidden-input or keyring support.

## Implemented cases

| Cases | What is exercised |
|---|---|
| Controlled prompt, username option / URL username (2) | Real CLI parser/source, password strategy, native database opening and engine query; prompt input is injected. |
| Controlled store, named / environment connection (2) | Real CLI and database authentication, with a controlled credential-store adapter. URLs stay credential-free and environment variables are restored. |
| Controlled prompt / store schema deployment (2) | Actual schema rendering, authenticated database opening and creation of a unique empty table. A separate connection verifies it and cleanup drops only that table. |
| Prompt / store dry run (2) | Actual schema rendering without source acquisition or connection creation. |
| Prompt/store, URL password, credential query option, username-alone conflicts (4) | Invalid-input exit 2 before either adapter is read or a connection is created. |
| Username mismatch from option / URL (2) | Conflicts after controlled store acquisition, with no database opening. |
| Missing prompt username / unattended prompt (2) | No adapter call or connection creation; missing credentials and interaction-required results remain explicit. |
| Cancellation during prompt / store acquisition (2) | Cancel after the controlled adapter starts; exit 130, safe output, no connection creation. |
| Missing / locked / unavailable controlled store (3) | Injected acquisition failures, safe JSON and no native connection. These do not prove real locked-store behavior. |
| Rejected stored credentials, human / JSON output (2) | A unique nonexistent principal reaches the real database, fails authentication, disposes its native connection and does not expose the generated password. No shared QA login is deliberately locked out. |
| Missing/unavailable real OS-store reference (1) | Production OS adapter, a unique nonexistent reference, safe failure and no connection creation. A missing backend is not a successful retrieval. |
| Real Windows stored credential authentication (1) | Production CredOS adapter and real database authentication using a temporary fixture-owned Credential Manager entry. |
| Real Windows secret preservation (1) | Production adapter round trip of special characters/trailing line terminators. This is an OS-store test, not a database login. |
| Real Linux Secret Service authentication (1) | Production CredOS adapter and real database authentication, only if an existing unlocked session is available. |
| Real macOS Keychain authentication (1) | Explicit skip: existing QA has no macOS runner or isolated Keychain fixture. |
| Real hidden-terminal login / Escape / schema deployment (3) | Actual CLI entry point and TerminalPasswordPrompt under a Linux pseudo-terminal, with initial terminal echo enabled. Successful input includes a backspace; output must not contain the password. Escape must return 130. Deployment uses a temporary schema file containing no credentials. |

All output checks use raw/encoded password comparisons without printing the expected secret. Real database operations use the existing native adapters. Controlled authentication sources are identified in test names and the sheet.

## Real backend safety and prerequisites

QA provisions random fixture-owned named credentials through `INamedCredentialStore`,
the same CredOS-backed interface used by the CLI. Windows and Linux lifecycle
fixtures create one entry with `AddAsync`, resolve it with `--credential`, and
remove it in async disposal. They contain no native credential-manager interop or
password-bearing helper process. Missing or inaccessible native stores are explicit
skips. Tests do not install, unlock or reconfigure the user's store.

The Linux terminal fixture uses util-linux `script`, the existing QA runtime/dependency manifests and the actual CLI executable assembly. Passwords are sent only through the pseudo-terminal input stream; they are absent from command arguments, configuration, environment and files. The transcript file is `/dev/null`. Windows ConPTY/macOS native-terminal fixtures are not implemented, so their real hidden-input cases remain skipped. Controlled console interactivity is not evidence for those native terminals.

## Existing database QA limitations

| QA suite | Live CLI consumers | Real deployment / backend limits |
|---|---|---|
| SQL Server | Controlled prompt/store and real Linux terminal cases | Existing Linux QA can deploy with its administrator login. Real Windows Credential Manager login is not exercised by this Linux workflow. |
| PostgreSQL | Controlled prompt/store and real Linux terminal cases | Native administrator can deploy. Real Secret Service login needs an already provisioned unlocked session. |
| MySQL | Controlled prompt/store and real Linux terminal cases | Native administrator can deploy. Same headless Secret Service limitation. |
| Timescale | Controlled prompt/store and real Linux terminal cases | Native administrator can deploy. Same Secret Service limitation. |
| Firebird | Controlled prompt/store; real Windows store where the runner's logon session supports it | Existing SELECT-only principal cannot deploy schema; Windows native terminal harness is unimplemented. |
| QuestDB | Controlled prompt/store and real Linux terminal login/Escape | Existing QA has no matching CLI schema-deployment recipe; public URL option restrictions are preserved. |
| CockroachDB | CLI policy/cancellation cases and real Linux terminal Escape | Existing insecure/passwordless fixture cannot establish password-authentication or authenticated deployment success. |
| CrateDB | CLI policy/cancellation cases and real Linux terminal Escape | Existing passwordless fixture cannot establish password-authentication or deployment success. |
| SQLite | Source-conflict/dry-run policies; OS-store preservation on Windows if run there | Username/password strategy is unsupported. Rendering without acquisition is not authentication success. |
| DuckDB | Source-conflict/dry-run policies; OS-store preservation where available | Username/password strategy is unsupported, including its ODBC QA. |
| Access | Source-conflict policies and real Windows store preservation where available | ODBC/OLE DB username/password strategy adapters are unsupported; no live CLI password login is claimed. |
| Excel | Source-conflict policies and real Windows store preservation where available | Same unsupported ODBC/OLE DB strategy limitation. |
| Text | Source-conflict policies and OS-store preservation where available | ODBC strategy adapter is unsupported. |
| Trino | Source-conflict policies | Native username/password strategy adapter is unsupported; ODBC fixture retains its licensed-driver ignore. |
| Power BI Desktop | Source-conflict policies and real Windows store preservation where available | ADOMD password strategy is unsupported; local-model access is not password authentication. |
| Drill | No executed live evidence | Existing proprietary-driver fixture remains ignored. |
| SSAS Multidimensional | No executed live evidence | Existing unprovisioned-server ignore remains; ADOMD password strategy is unsupported. |
| SSAS Tabular | No executed live evidence | Same unprovisioned-server/ADOMD limitation. |
| SingleStore | Native controlled prompt/store and real Linux terminal login/Escape | Upstream #1709 supplies a license-free fixture. A CLI deployment/dry-run recipe for this family is not yet enabled; ODBC fixtures remain ignored. |
| Oracle | Native controlled prompt/store and real Linux terminal login/Escape | Upstream #1709 supplies the fixture; a CLI authentication deployment/dry-run recipe is not yet enabled. No native Secret Service pass is inferred from terminal login. |

There is no macOS provider QA workflow. Native Keychain retrieval, real locked/unavailable-store UI behavior, Windows/macOS no-echo terminal behavior and native backend cancellation are not inferred from controlled-adapter tests. These remain explicit verification gaps. No CI configuration is changed to manufacture a passing platform result.

## Certificate CLI acceptance fixtures

`CliCertificateTests` exercises parsing, shared Library mapping, hidden-prompt and fake OS-store sources, independent supplemental database credentials, dry runs, disposal, cancellation and safe errors. These tests do not claim live database authentication or native OS-store access.

`CliCertificateLiveTests` is opt-in through fixture environment settings: `DUBURL_CLI_CERT_URL` (no password, CA or certificate options; disable pooling), `DUBURL_CLI_CERT_PEM`, `_KEY`, `_PFX`, `_P12`, and `_CA` (each suffix uses the `DUBURL_CLI_CERT` prefix). `_PASSWORD_REFERENCE` optionally selects an existing DubUrl named credential for encrypted bundles. Negative cases additionally require `_REJECTED_PEM`/`_REJECTED_KEY`, `_UNTRUSTED_CA`, or `_WRONG_HOST_URL`. Use a certificate-required isolated account and unencrypted PEM for the negative cases. Missing prerequisites are explicit skips. No machine configuration or external account is created by these tests.

The Library's external `ClientCertificateLiveTests` uses `DUBURL_<DATABASE>_CERT_URL`, `_PEM`, `_KEY`, and `_CA` (POSTGRESQL, COCKROACHDB, TIMESCALEDB, MYSQL, MARIADB, SINGLESTORE). Supply CA trust separately from the URL. The existing isolated PostgreSQL provider QA also exercises PEM, encrypted PEM, PFX/P12, rejected identity, wrong CA, hostname mismatch, reopening, pool isolation and rotation. Its automatic Docker fixture requires the provisioned Linux runner; Windows/macOS skip that fixture.
