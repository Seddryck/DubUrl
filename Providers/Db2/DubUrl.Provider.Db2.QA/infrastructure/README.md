---
layout: doc
title: Running provider coverage suites
permalink: /provider-qa-coverage/
---

These suites use PowerShell 7 and the existing provider QA harness. Full runs
default to .NET SDKs 8.0.416, 9.0.308 and 10.0.401. Select a framework with
`-frameworks @('net10.0')`; `-sdkVersions` accepts a hashtable mapping frameworks
to installed SDK versions. Run commands from the repository root.

| Suite | Command | Prerequisites |
| --- | --- | --- |
| Db2 | `./QA/DubUrl.QA/Db2/deploy-db2-test-env.ps1` | Linux amd64 Docker engine, privileged container support, sufficient memory/disk for Db2. Linux hosts need libxml2, libpam and libaio for IBM's CLI driver. |
| MariaDB | `./QA/DubUrl.QA/MariaDb/deploy-mariadb-test-env.ps1` | Docker and MariaDB ODBC driver. Linux CI installs odbc-mariadb/unixODBC and uses a process-local registry alias. |
| PostgreSQL discovery | `./QA/DubUrl.QA/Postgresql/deploy-postgresql-test-env.ps1 -container -testFilter 'TestCategory=Discovery'` | Docker; omit the filter for the full suite with PostgreSQL ODBC installed. |
| SQL Server discovery | `./QA/DubUrl.QA/MsSqlServer/deploy-mssqlserver-test-env.ps1 -container -testFilter 'TestCategory=Discovery'` | Docker; omit the filter for the full suite with SQL Server ODBC installed. |
| SQL Server OLE DB | `./QA/DubUrl.QA/MsSqlServer/deploy-sqlserver-oledb-test-env.ps1` | Windows x64, SQL Server LocalDB, sqlcmd and Microsoft OLE DB 19. |
| Oracle OLE DB | `./QA/DubUrl.QA/Oracle/deploy-oracle-oledb-test-env.ps1 -oracleHome C:/path/to/dbhomeFree` | Windows x64, Oracle Free on localhost:1521/FREEPDB1, Oracle OLE DB and the matching x64 client. Set DUBURL_ORACLE_QA_ADMIN_PASSWORD privately for SYSTEM. |

Db2 and MariaDB use generated passwords, isolated Compose projects, bounded SQL
readiness and cleanup on failure/success. Db2 uses IBM Community Edition and
explicit license acceptance; its container requires privileged mode. The tested
manifest is pinned to e48ca934c29ab72d9e8b602929e6a52b64841cb7f8ef9426ee8cf5e1bc3e5fb3.
MariaDB's Linux amd64 manifest is pinned to
d9fc02976678d4236145299c40dc267051681b33489c994f0ec4a097ce4a9521.
Use `-port` to avoid host port conflicts. Db2 permits 1080 seconds for initial
database setup; MariaDB permits 300. These are functional tests, not benchmarks.
Db2's shared ANSI interval contract is explicitly excluded; date and time return
conversions are tested. Db2 schema tests exercise basic DDL, typed/null round trips
and dictionary metadata, without claiming an extensible metadata reader.

MariaDB's fixture now runs on MariaDB and asserts server identity. Its ODBC tests
were moved out of the MySQL suite. MySQL retains its own native and ODBC tests;
the existing MySQL ODBC compatibility alias remains a driver compatibility test
against MySQL. A native-only local MariaDB run can use
`-testFilter 'TestCategory!=ODBC'`; full CI requires the ODBC category to execute.

Discovery runs set DUBURL_POSTGRESQL_DISCOVERY_QA_CONNECTION_STRING or
DUBURL_SQLSERVER_DISCOVERY_QA_CONNECTION_STRING from the provisioned container
credentials, and restore prior values afterward. CI runs the Discovery category
separately and rejects missing, skipped or failed tests. Existing databases provide
the schema/table creation privileges required by the shared discovery contract.
Do not print these connection variables.

Windows jobs execute only the OleDB category. SQL Server uses an isolated LocalDB
instance with Windows authentication and removes the instance after testing.
Oracle uses a dedicated, uniquely named fixture schema and drops it afterward.
Windows CI provisions native Oracle Free, rather than attempting Linux containers
on a Windows runner. Native database/client installations exist only on disposable
GitHub runners, which are discarded after each job. The local commands never
install or register machine-wide software automatically.

The Windows workflow passes `-installPrerequisites` to install checksum-pinned
Microsoft OLE DB 19.4.3 or Oracle Free/ODAC 23.26.3. That switch is restricted to
GitHub Actions; local machines require explicit prerequisite installation by their
owner. Oracle's temporary installer response contains a generated password, stays
outside the checkout, is removed in finally, and is never uploaded. Windows hosted
runner VC++ runtimes and LocalDB/sqlcmd are prerequisites checked by the jobs.
The SQL Server script's optional `-providerName` is for explicit local compatibility
testing; CI uses provider discovery with the current Microsoft driver installed.

| CI decision | Evidence and choice |
| --- | --- |
| Frameworks | Repository support is net8.0/net9.0/net10.0; new jobs expose a framework matrix with matching SDK selection. |
| Runners | Db2/MariaDB images are Linux amd64; their CI uses Ubuntu 24.04. OLE DB requires Windows COM/client registration and runs on Windows 2025 x64. |
| Deliverables | Existing framework-dependent QA adapters and infrastructure bundles; repository version, portable RID, no new NuGet package or executable distribution. |
| Triggers | Pull requests, pushes to main and manual runs. Read-only repository permission; no publishing or external account credentials. |
| Validation | Stale TRX files are removed. Every requested suite must execute tests; required categories reject skips, failures and zero-test filters. |

Results are in `.test-results/<suite>/<framework>/`. Existing QaBundle targets
package test adapters, drivers, manifests and provider-owned infrastructure under
each project's `bin/qa/`. OLE DB jobs reuse their provider's bundle and upload it
under a distinct workflow artifact name. Drivers and Oracle Free are downloaded
from their publishers; installer archives are not redistributed in the bundles.

Reference documentation: [IBM container setup](https://www.ibm.com/docs/en/db2/12.1.x?topic=system-linux),
[MariaDB images](https://mariadb.com/docs/server/server-management/automated-mariadb-deployment-and-administration/docker-and-mariadb),
[Oracle Free silent setup](https://docs.oracle.com/en/database/oracle/oracle-database/26/xeinw/installing-oracle-database-xe.html),
[Oracle ODAC](https://www.oracle.com/database/technologies/net-downloads.html),
[Microsoft OLE DB setup](https://learn.microsoft.com/en-us/sql/connect/oledb/applications/installing-oledb-driver-for-sql-server).
