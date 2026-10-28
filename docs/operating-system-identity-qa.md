# Operating-system identity QA (#1700)

All 22 existing provider QA projects inherit the same 37 named cases through the native/ODBC/OLE DB/ADOMD contracts. This includes upstream MariaDB and Db2 QA and the existing Windows OLE DB jobs merged unchanged from main. Workflows, deployment scripts and the QA runner have no task-specific changes. Configuration/unsupported guards are not successful database authentication.

## Controlled PostgreSQL peer

On the existing native PostgreSQL Linux Actions runner, QA reuses the already-pulled pinned PostgreSQL 17 image. No new image pull, package installation or machine configuration is performed. The installed .NET SDK/runtime and compiled QA directory are mounted read-only. The test process and PostgreSQL run in the same disposable container, with a fixture-owned OS user alice. The container has no network. PostgreSQL listens only on `/tmp/duburl-identity-socket` port 55432.

Initialization uses local peer, never trust or a password. Tested HBA rules are `local all all peer map=fixture` followed by `host all all all reject`. The explicit mapping permits alice -> alice and alice -> mapped_alice, and postgres -> postgres for administration. No mapping permits alice -> bob. Roles have no password. A client role is a request, not a client-side OS identity comparison.

Ten child NUnit cases execute the actual current built strategy/native driver: sync and async login/query, mapped-role login, unmapped-role rejection/disposal, omitted-role native default, physical pool reuse, pool-wait cancellation/disposal, close/reopen, missing-socket rejection/disposal, and isolation of an unmapped role from a cached authenticated session. Success checks current_user, session_user and the absence of a TCP client address; the controlled HBA proves peer. The child uses the already-installed VSTest console, emits actual TRX, and the outer QA case requires its exact named child outcome to be Passed. Missing infrastructure, missing/duplicate cases, skips or failures cannot become a successful live case. One container executes all ten cases per framework. It and only its volumes and validated UUID-owned temp directory are removed. No global connection pool is cleared.

Local Linux container execution is opt-in: `DUBURL_IDENTITY_QA_PEER_CONTAINER=1 dotnet test Providers/PostgreSql/DubUrl.Provider.PostgreSql.QA -c Release -f net10.0 --filter TestCategory=OperatingSystemIdentityAuthentication`. This requires an already provisioned Docker host, the exact existing QA image and a complete installed SDK. Windows skips Unix-socket peer.

## Windows LocalDB

Six live native SqlClient scenarios are implemented: sync/async login and query, physical pool reuse, pool-wait cancellation and disposal, close/reopen and missing-instance failure/disposal. A UUID-owned LocalDB instance is created/started and stopped/deleted. No default instance or existing database is changed. A parameterized query compares ORIGINAL_LOGIN, SUSER_SNAME and SYSTEM_USER with the current WindowsIdentity, checks NTLM and reads @@SPID; names are not printed. The URL overrides its placeholder host with escaped `(localdb)\\<owned-name>` through Data Source. Only owned pools are cleared.

Run on a Windows host with SqlLocalDB.exe already installed: `dotnet test Providers/SqlServer/DubUrl.Provider.MsSqlServer.QA -c Release -f net10.0 --filter TestCategory=OperatingSystemIdentityAuthentication`. Missing LocalDB is explicitly skipped, never installed by QA. **Existing SQL Server provider Actions runs on Linux, so all six LocalDB cases skip there.** Local results are reported separately and cannot put a v in the Actions sheet.

## Configuration and unsupported cases

Shared cases verify lazy metadata and URL-only Parse, sync/async closed connections and caller ownership, strict native settings, explicit empty/nonempty URL-password rejection, nine competing query aliases (including empty values), compatible SQL true/SSPI settings, SQL username rejection with sync/async native disposal, peer TCP/multihost rejection and disposal, and pre-cancelled acquisition without native creation. Unsupported provider/family/platform checks fail before creation. Peer scope is native PostgreSQL only: Npgsql usage by CockroachDB, Timescale, QuestDB or CrateDB does not establish an implemented peer contract. SQL Server native integrated configuration on Linux remains allowed; LocalDB itself is Windows-only.

MySQL, SingleStore, Oracle, Firebird, file databases, Trino, Drill, Power BI/SSAS and ODBC/OLE DB/ADOMD have no implementation of this shared strategy. Their authentication positives skip with a reason; unsupported-strategy and applicable conflict guards still run. File URL userinfo cases skip because the URL grammar rejects them before strategy validation.

The existing Windows OLE DB jobs strictly select `TestCategory=OleDB` and require every selected test to pass. That category remains on the three original transport cases and Oracle's provider-specific scalar case, rather than leaking onto every inherited authentication case. This preserves their original required transport checks without treating unsupported authentication skips as successful logins or changing workflow/runner filters. The broader authentication cases remain available in unfiltered QA; the focused Windows OLE DB jobs do not verify them.

No external credential acquisition exists for this strategy. Token/certificate/password sources belong to other selected strategies, and every query attempt to combine competing authentication settings is rejected. Reusing a pooled physical session is not a new OS handshake. Switching process identity/impersonation is out of scope. Remote SQL Kerberos, delegation/domains, TCP ident, PostgreSQL GSSAPI/SSPI, Windows peer, physical-handshake cancellation, and arbitrary-server policy verification are unverified. Pool-wait cancellation is named separately. No claim is made that client configuration can distinguish peer from trust on an arbitrary server.

GitHub Actions results are recorded in the PR and authentication QA workbook after complete TRX inspection, separately from unit tests and local checks.
