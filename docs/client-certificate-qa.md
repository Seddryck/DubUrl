# Client-certificate provider QA (#1699)

Every existing native/ODBC/OLE DB/ADOMD QA fixture inherits the same 39 ClientCertificateAuthentication cases. The existing complete QA runs discover them; no workflow or QA runner changes are required. Configuration and unsupported guards are separate from live certificate authentication.

## Cases and boundaries

| Cases | What a pass verifies |
|---|---|
| Availability (1) | The selected native adapter installs its hook; unsupported provider/family combinations reject before mapping, construction or supplemental acquisition. |
| Sync/async Connect (2) | Actual closed native connections, lazy Parse/metadata, caller-owned certificate after disposal. No TLS handshake is claimed. |
| Native hook selection (1) | Controlled invocation selects exactly the caller's certificate object, clears competing candidates and enforces VerifyFull. This is not server authentication. |
| Unsupported supplemental combination (1) | Unsupported provider/combination rejects without acquiring credentials or creating a connection. |
| Explicit supplemental source, sync/async (2) | Parse/metadata remain lazy; one source resolution per logical connection; actual native builder receives the secret; parsed caches do not; caller retains certificate/source ownership. |
| Supplemental failure/cancellation/async-only sync rejection/identity mismatch (4) | Safe errors, zero created connections and no fallback. Cancellation starts after the controlled source is entered. |
| Missing private key/disposed certificate (2) | InvalidCertificate before native creation. |
| Disabled/required-only/CA-only TLS (3) | Weaker modes reject; actual failed native connections must reject reopening with ObjectDisposedException. Merely observing Closed or Component.Disposed is insufficient across drivers. |
| Password/PWD/client certificate/key/key-password aliases (6) | Conflicts before acquisition/construction; raw/encoded sentinel and unsafe inner exceptions are absent. |
| URL password/pre-cancellation (2) | Conflict/cancellation before connection creation. File-URL userinfo grammar cannot exercise this conflict and explicitly skips that variation. |
| MariaDB native alias (1) | MySqlConnector QA constructs the MariaDB native hook without opening a database. No live MariaDB result is claimed. |
| Live sync/async PostgreSQL certificate login (2) | Actual certificate-only authentication and SELECT 1, with server-reported TLS client serial and no database password. |
| Live missing/untrusted/wrong-identity/expired client (4) | Fresh physical connections cannot authenticate; applicable strategy failures are sanitized, native connections disposed and client certificates remain caller-owned. |
| Live untrusted server CA/hostname mismatch/plaintext (3) | Server trust/name enforcement and cert-only host rules reject each weaker/invalid path. |
| Live pool reuse/rotation/pool-wait cancellation/reopening/cached-identity distinction (5) | Actual native sessions/serials establish reuse and replacement; only the owned pool is cleared. Pending native pool acquisition cancels/disposes while the held session remains usable. A reused old session is explicitly not acceptance of a replacement certificate. |

## Isolated PostgreSQL fixture inside existing QA

On the native PostgreSQL Linux Actions runner, the live cases create one additional UUID-owned certificate-only PostgreSQL container per framework. They reuse the exact pinned postgres:17 image already pulled by the existing provider workflow; no image/software installation, host service reconfiguration or workflow change is added. The listener is published only on a random loopback port. Other live databases are not silently replaced with PostgreSQL.

QA generates a private CA, localhost server certificate, valid/replacement client certificates, wrong-identity/expired client certificates and an unrelated CA/client in memory. Application/client private keys never leave their certificate objects. Only generated **server fixture** key material is written to a new mode-700 temporary directory outside the repository, with key files mode 600. The non-secret initialization script is readable by the container's postgres user. Server material is copied into the owned container with suitable ownership/permissions. Production strategy code does not export keys or write certificate files.

The database has a passwordless alice role and an explicit hostssl app/alice cert rule followed by host/all/reject. Local peer access exists only for image initialization; the tested network path cannot match a password/trust fallback. The driver trusts only fixture-local CA material and verifies localhost. Its Require Auth=None setting forbids database-password fallback. Successful sessions must report their actual client serial from pg_stat_ssl.

Each negative case disables pooling. Pool cases use a unique application name and size-one pool, server process IDs and client serials. Replacement certificates have the same valid identity but different serial/key; after clearing only that pool, a new physical connection must report the replacement serial and subsequently reuse it. The cached-identity case deliberately changes to a wrong-identity certificate without clearing, observes the old authenticated session, then clears and requires rejection on the fresh connection. This demonstrates why cached-session success is not certificate validation or revocation.

Teardown removes only the UUID-owned container/anonymous volumes and verified owned temporary directory, then disposes fixture certificates. No Docker prune, global pool clearing, machine certificate-store change, private-key logging or shared QA account mutation is performed.

Local container runs are explicitly opt-in with DUBURL_CERT_QA_POSTGRES_CONTAINER=1 on an existing Linux Docker host with that pinned image available. Windows/macOS automatic container fixtures are not implemented. Routine local configuration tests skip live cases. Existing CI supplies Docker automatically; missing expected infrastructure fails rather than becoming a false live pass.

## Existing QA support and unsupported cases

| QA database/transport | Native configuration | Live certificate-required fixture |
|---|---|---|
| PostgreSQL / Npgsql | Supported | Isolated cert-only fixture above, executed by existing Linux QA. |
| CockroachDB / Npgsql | Supported | Existing ordinary QA uses an insecure cluster, not a secure user-certificate target. Live cases explicitly skip. |
| MySQL / MySqlConnector | Supported, including explicit supplemental source | Existing password QA does not provision a trusted certificate-required account with REQUIRE X509/SUBJECT/ISSUER. Live cases explicitly skip. |
| MariaDB / MySqlConnector | Native alias/hook check under MySQL QA; existing five-adapter unit tests remain separate | No separate native MariaDB QA server/workflow; its ODBC fixture does not establish this support. Live cases are unverified. |
| SingleStore / SingleStoreConnector | Supported, including supplemental source | License-free upstream fixture does not configure certificate-required identities/trusted TLS; no MySQL-equivalence claim. Live cases explicitly skip. |
| Timescale, QuestDB, CrateDB / Npgsql | Unsupported by this scoped adapter | Shared PostgreSQL wire protocol is not certificate-identity integration. |
| MySql.Data, SQL Server, Oracle, Firebird, Trino | Unsupported by this strategy | SQL token/Entra upstream certificates and other mechanisms are separate. |
| SQLite, embedded DuckDB, Access, Excel, Text | Unsupported | File access is not a database TLS handshake. |
| ODBC/OLE DB/ADOMD, Power BI, SSAS, Drill | Unsupported | Existing proprietary-driver/platform/unprovisioned-server fixture ignores also apply. |

The other four required database adapters retain actual native configuration tests and the pre-existing opt-in ClientCertificateLiveTests for externally provisioned certificate-required targets. Their live acceptance is **not** inferred from hook installation, an ordinary password login or PostgreSQL's result. No additional native MariaDB server is provisioned by this change.

Mismatched key/certificate pairs cannot be constructed through the supported X509Certificate2 key-binding API; they are rejected at import rather than exercised as a fake database failure. Encrypted-key-file loading, TLS algorithm/version matrices, CA/CRL revocation, identity remapping and certificate-plus-password live account-plugin semantics remain outside this QA's verified scope. Rotation proves a replacement handshake, not revocation of an already authenticated session. Physical TLS-handshake cancellation remains unverified; pool-wait cancellation and pre-cancellation are named separately.

The workbook includes only provider QA. A v requires the same case/fixture to pass on .NET 8, 9 and 10; unsupported, skipped and unrun cases remain X. Unit tests are reported separately. Native behavior: [PostgreSQL certificate authentication](https://www.postgresql.org/docs/current/auth-cert.html), [Npgsql TLS](https://www.npgsql.org/doc/security.html), [MySqlConnector TLS options](https://mysqlconnector.net/connection-options/).
