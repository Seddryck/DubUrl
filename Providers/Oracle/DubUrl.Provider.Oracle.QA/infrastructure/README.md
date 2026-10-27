# Oracle provider QA

From the repository root, with PowerShell 7, Docker Compose v2 and the .NET SDKs
8.0.416, 9.0.308 and 10.0.401 installed:

```powershell
./QA/DubUrl.QA/Oracle/deploy-oracle-test-env.ps1
```

Use `-frameworks @('net10.0')` for a focused run, `-port 1522` if 1521 is busy,
and `-readinessTimeoutSeconds 1200` for slower hosts. An optional `-sdkVersions`
hashtable selects locally installed SDKs for each requested framework.

The same command runs locally and in CI; no database installation, registry login
or paid license is required. Each invocation uses an isolated Compose project,
fresh volume, generated administrator/test passwords, and a dedicated `duburl_qa`
schema in `FREEPDB1`. Administrative access is used only to provision the schema.
Tests use `DUBURL_ORACLE_QA_URL`, which the script sets and restores automatically.
Do not print that variable or run `docker compose config` with credentials set.

## Image and platform

The image is Oracle Database Free Lite `23.26.1.0-lite-amd64`, pinned to
`sha256:ef1a38683b3783b80e033be6b8f2cb31299dcba5430514ec96e2e8f4f0307d15`.
Its registry manifest declares Linux/amd64. Lite provides the relational SQL,
PL/SQL, dictionary views and SQL*Plus needed here, with less download overhead
than the full image. CI uses Ubuntu 24.04 x64. Windows hosts need Docker Desktop
with Linux containers; ARM hosts require amd64 emulation and are not CI-validated.
Allow at least 2 CPUs, 4 GB of Docker memory and 10 GB free disk space for the
image/database and build outputs. Oracle Free limits include 2 CPUs, 2 GB database
memory and 12 GB user data; fixtures stay far below them.

Readiness requires the image's completed startup hook followed by a successful
SQL query in `FREEPDB1`; the default deadline is
960 seconds after image pulling. Startup or test failures print redacted container
logs, fail the command and still attempt to delete the container and volume.
For startup problems, check Docker's Linux engine, available disk/memory, registry
network access, and port conflicts. If interrupted forcibly, remove the remaining
`duburl-oracle-*` Compose project and its volume using Docker Desktop.

## Coverage and limits

The suite exercises the shared provider contract, Oracle named/repeated parameter
binding, authentication failure, NUMBER/VARCHAR2/DATE/TIMESTAMP and NULL/empty-string
behavior, row limiting, template rendering, and basic rendered table creation,
dictionary column discovery and table removal. Oracle scalar queries use `dual`.
The shared positional `?`, standalone TIME and ANSI interval contracts are explicitly
skipped: they do not express the Oracle APIs/syntax tested here. Oracle intervals
and time-zone types are not claimed as covered. ODBC/OLE DB, bulk ingestion,
advanced DDL and the extensible schema-discovery API are outside this suite; metadata
assertions query Oracle's own dictionary. This is functional QA, not benchmarking.

Results are under `.test-results/oracle/<framework>/`; bundles are under
`Providers/Oracle/DubUrl.Provider.Oracle.QA/bin/qa/oracle/<version>/<framework>/portable/`.
Bundle infrastructure includes the pinned Compose definition, initialization SQL
and this guide. When running a bundled adapter against an existing test database,
set `DUBURL_ORACLE_QA_URL` to a dedicated fixture schema before invoking its tests.

## CI decisions

| Dimension | Evidence and decision |
| --- | --- |
| Frameworks | `Directory.Build.props` supports net8.0/net9.0/net10.0; run each in its own job with its matching SDK. |
| Runner | Pinned database image is Linux/amd64; use Ubuntu 24.04 x64. No Windows/macOS or ARM execution is claimed by the manifest. |
| Deliverable | Portable, framework-dependent QA adapter and infrastructure bundle; no NuGet package, self-contained executable, AOT or additional RID matrix. |
| Version | Existing `QaBundle` target uses the repository version; one bundle per framework, including driver, test host and manifest. |
| Triggers and permissions | Pull requests, main pushes and manual runs; read-only repository access, no publication or license secrets. |
| Prerequisites | Docker Compose v2 and the selected SDK; no generated product assets or machine-wide driver installation. |
| Validation | Require executed tests in both complete and Schema TRX reports; upload results and bundles for each framework. |

References: [Oracle image registry](https://container-registry.oracle.com/ords/ocr/ba/database/free),
[Oracle container guide](https://docs.oracle.com/en/database/oracle/agent-memory/26.8/guide/run-locally.html).
