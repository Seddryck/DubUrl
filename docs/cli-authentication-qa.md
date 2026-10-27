# CLI password and credential-store QA (#1690)

Every existing provider QA fixture inherits the same 31 `CliPasswordAuthentication` cases. Existing complete QA runs discover them without workflow or runner changes. Results distinguish controlled adapters from real terminal/store smoke tests; a fake adapter pass never establishes native hidden-input or keyring support.

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
| Real Windows stored credential authentication (1) | Production CredReadW adapter and real database authentication using a temporary fixture-owned Credential Manager entry. |
| Real Windows secret preservation (1) | Production adapter round trip of special characters/trailing line terminators. This is an OS-store test, not a database login. |
| Real Linux Secret Service authentication (1) | Production secret-tool adapter and real database authentication, only if an existing unlocked session is available. |
| Real macOS Keychain authentication (1) | Explicit skip: existing QA has no macOS runner or isolated Keychain fixture. |
| Real hidden-terminal login / Escape / schema deployment (3) | Actual CLI entry point and TerminalPasswordPrompt under a Linux pseudo-terminal, with initial terminal echo enabled. Successful input includes a backspace; output must not contain the password. Escape must return 130. Deployment uses a temporary schema file containing no credentials. |

All output checks use raw/encoded password comparisons without printing the expected secret. Real database operations use the existing native adapters. Controlled authentication sources are identified in test names and the sheet.

## Real backend safety and prerequisites

The production credential-store adapter remains read-only. QA provisioning is separate: Windows writes one uniquely named generic entry with session-only persistence, reads that exact entry and removes it in `finally`. It never enumerates or reads existing user entries. Password bytes are passed in memory and the temporary unmanaged blob is zeroed. See [CREDENTIALW](https://learn.microsoft.com/en-us/windows/win32/api/wincred/ns-wincred-credentialw).

Linux provisioning uses `secret-tool store` with the password on stdin, only after checking for the existing utility, a session bus, a default collection and its unlocked state. Only the unique `service=duburl, entry=<fixture reference>` item is cleared. Tests do not install a keyring, start/unlock a user collection or change the session configuration. Missing prerequisites are explicit skips.

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
| SingleStore | No executed live evidence | Existing manual QA requires the absent license secret; ODBC fixtures also remain ignored. |

There is no macOS provider QA workflow. Native Keychain retrieval, real locked/unavailable-store UI behavior, Windows/macOS no-echo terminal behavior and backend helper-process cancellation are not inferred from controlled-adapter tests. These remain explicit verification gaps. No CI configuration is changed to manufacture a passing platform result.
