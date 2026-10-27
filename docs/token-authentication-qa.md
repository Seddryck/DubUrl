# Provider-native token authentication QA (#1698)

All 20 existing provider QA projects inherit 25 `NativeTokenAuthentication` cases per driver fixture. Existing complete QA runs discover them without workflow, runner, or CI changes. Passing configuration, unsupported-provider and conflict guards are not successful token authentication.

## Implemented QA boundaries

| Cases | What a pass establishes |
|---|---|
| Availability (1) | The fixture's real native provider accepts the SQL Server callback, or explicitly rejects the unsupported strategy before URL mapping, connection creation or source invocation. |
| Synchronous/asynchronous Connect (2) | Actual SqlClient construction is lazy; Parse/metadata/configuration do not acquire tokens; connections remain closed; password/fixed-token/integrated modes are absent; certificate/hostname validation and encryption remain enabled. Disposal leaves the caller-owned source alive. |
| Callback identity (1) | Two actual closed SqlConnections from one pooled strategy share its delegate; a separately constructed strategy has another delegate. This does not prove physical pool reuse or refresh. |
| Userinfo, Password, PWD, fixed token, Authentication and integrated-security conflicts (6) | Rejection before native connection creation or token acquisition. Raw/encoded sentinel values and unsafe inner exceptions are absent from the authentication error. |
| Pre-cancelled Connect (1) | Cancellation occurs before creating a connection or acquiring a token. This is not cancellation during token acquisition. |
| Ordinary SQL-container rejection (1) | Real opening fails safely before a token source is called, disposes its native connection and does not fall back to the fixture password. The ordinary container lacks the Entra configuration/trusted endpoint required by the strategy. This is negative I/O evidence, not a successful Entra login or a specific server-error classification. |
| Certificate source configuration (1) | A transient, memory-only self-signed certificate can configure the optional source and actual native callback without acquisition, exporting its private key, or disposing the caller's certificate/source. It is not registered with Entra. |
| Untrusted resource/authority (2) | Explicit source validation rejects a mismatched endpoint without contacting it; the caller certificate remains usable. These are controlled adapter checks, not live certificate authentication. |
| Live sync/async certificate login (2) | Opt-in certificate-backed Entra acquisition, actual native callback invocation, database opening and SELECT 1. |
| Live authenticated pool reuse (1) | An isolated size-one native pool returns the same server session without another callback. This is cached-session reuse, not token refresh. |
| Live unregistered/expired certificate (2) | After a positive query, a new source/MSAL cache and non-pooled physical connection reject a freshly generated unregistered or expired certificate. No successful cached token can mask the negative. |
| Live callback failure/expired token/cancellation (3) | An Entra-capable server drives the real native callback; controlled sources fail, return an expired token or block until cancellation. Actual failed connections are disposed, errors are sanitized and sources remain caller-owned. These do not prove real Entra outage/cancellation behavior. |
| Live replacement certificate (1) | A second registered certificate is used through a fresh source/cache and a distinct pool/physical connection, followed by reuse of its authenticated pool. Only fixture-owned pools are cleared. |
| Expiry-driven native refresh (1) | Explicit skip: no existing fixture controls Entra token lifetime or the native refresh clock. Real expiry-driven renewal remains unverified. Delegate identity, cached tokens and session reuse are not substituted. |

## Existing QA and unsupported cases

Only native **Microsoft.Data.SqlClient** has the #1698 adapter. Native PostgreSQL/Timescale/QuestDB/CockroachDB/CrateDB, MySQL, Firebird, SingleStore, SQLite, DuckDB and Trino have no adapter for this strategy. SQL Server ODBC/OLE DB, the other ODBC/OLE DB fixtures, Access, Excel, Text and ADOMD (Power BI/SSAS) also lack it. Their availability case verifies rejection; SQL-specific cases explicitly skip. Other authentication mechanisms those drivers might support are outside this implementation.

SQL Server's existing password-authenticated container is not an Entra-enabled target. The unchanged workflows inject no Entra tenant/application identity, registered certificate/private key or trusted SQL endpoint. Therefore automatic QA can verify construction, TLS policy, guards, source ownership and safe ordinary-container failure, but **cannot verify a successful token login, native server request mapping, token expiration propagation during login, authenticated pool reuse, certificate rejection/rotation or real refresh**. Live rows remain skipped/X until their external prerequisites exist. See [the native SqlClient requirements](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql/azure-active-directory-authentication).

Existing proprietary-driver and unprovisioned SSAS fixture ignores still apply. Upstream #1709 added Oracle QA and made SingleStore QA license-free; both now run automatically and inherit these cases, but neither native driver has the SQL Server token adapter. These are strategy exclusions, not token authentication passes. There is no macOS provider QA workflow; no new platform coverage is implied.

The userinfo-conflict variation skips for SQLite, DuckDB, Access, Excel and Text: their file URL grammar rejects userinfo before strategy conflict validation. Query-option conflicts and pre-cancellation remain independently executable; a grammar rejection is not substituted for a strategy conflict pass.

## Opt-in live fixture

The live cases are normally discovered and explicitly skipped, rather than hidden behind NUnit Explicit. An already provisioned host may set `DUBURL_ENTRA_QA=1` and supply these process-environment values outside source control:

- `DUBURL_ENTRA_CERT_SQL_URL`: credential-free native `mssql://` URL for an Entra-enabled SQL database with a trusted server certificate/hostname.
- `DUBURL_ENTRA_CERT_CLIENT_ID`, `DUBURL_ENTRA_CERT_AUTHORITY`, `DUBURL_ENTRA_CERT_RESOURCE`: application ID, explicitly trusted tenant authority and target SQL audience.
- `DUBURL_ENTRA_CERT_PEM`, `DUBURL_ENTRA_CERT_KEY`: paths to the registered application certificate and its private key, readable by the fixture process.
- `DUBURL_ENTRA_ROTATION_PEM`, `DUBURL_ENTRA_ROTATION_KEY`: optional second registered certificate/key for the same application, required only for rotation.

The application identity must have database query permission. The fixture does not provision identities, modify Entra or machine certificate stores, install services, or change CI. Each fixture loads its own certificate and fresh MSAL client/cache; tests do not print credentials, tokens or private keys. Invalid test certificates are generated in memory. Pooling is off except in explicitly named isolated pool tests. Cleanup targets only the created connections/pools and loaded fixture objects; caller-owned application resources are not disposed by the strategy. Clear the opt-in environment after use.

Existing unit tests remain separate from this QA inventory. A green ordinary QA workflow with skipped live cases does not satisfy #1698's live Entra acceptance criteria. The workbook contains only provider QA cases and uses v only for the same fixture passing on .NET 8, 9 and 10.
