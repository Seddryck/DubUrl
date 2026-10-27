# SingleStore provider QA

With PowerShell 7, Docker Compose v2 and the .NET SDKs 8.0.416, 9.0.308 and
10.0.401 installed, run from the repository root:

```powershell
./QA/DubUrl.QA/SingleStore/deploy-singlestore-test-env.ps1
```

Use `-frameworks @('net10.0')` for a focused run, `-port 3307` for a busy host
port, or `-readinessTimeoutSeconds 600` for slower startup. `-sdkVersions` accepts
a hashtable mapping requested frameworks to installed SDK versions. The legacy
`run-singlestore-docker.cmd` forwards to this full QA command; `-force` is retained
as a compatibility argument and every invocation runs the suite.

The same command provisions and tests SingleStore locally and in CI. No license
key, repository secret or registry login is required; `SINGLESTORE_LICENSE` is
not forwarded to the container. The script generates a root test password,
configures `DUBURL_SINGLESTORE_QA_URL`, and restores environment variables when it
finishes. It creates an isolated Compose project and removes its container and
anonymous volumes on both success and failure. Do not print the connection URL
or run `docker compose config` with the generated credentials set.

## Image and prerequisites

The image is `ghcr.io/singlestore-labs/singlestoredb-dev` pinned to Linux/amd64
manifest `sha256:7ef945c3bf93e6b50b17cd8f5fbfa6f304f55ef04f45ccf408bbf051430a9457`.
It bundles engine 9.1.1 (`626ec146c9`); no runtime version override/download is
requested. Update the digest and revalidate QA together when upgrading the engine.

This image requires x86-64-v3 CPU instructions. CI uses Ubuntu 24.04 x64.
Windows hosts need Docker Desktop's Linux engine; ARM emulation depends on the
host supporting those instructions and is not validated by CI. Compose caps the
container at 2 CPUs and 4 GB memory, below the Dev Image's host limits of 8 cores
and 64 GB. Allow additional memory for builds and sufficient disk for the image.
Studio is disabled and only the MySQL-compatible port is exposed on loopback.

The deterministic fixture explicitly creates a two-partition database, respecting
the Dev Image partition limit. Readiness requires an authenticated SQL query,
with a 300-second default deadline after image pulling. Failures print redacted
container logs and attempt cleanup. Check Docker's Linux engine, CPU instruction
support, memory, disk, registry connectivity and host port availability if startup
fails. If forcibly interrupted, remove the remaining `duburl-singlestore-*`
Compose project through Docker Desktop.

## Coverage and artifacts

The suite covers connection resolution, authentication failures, the shared query,
parameter, template and row-limiting contracts, decimal/date/microsecond timestamp/
Unicode/null behavior, rendered table creation/removal, dictionary column metadata
and the two-partition database configuration. A separate test checks SingleStore's
promotion of auto-increment integers to BIGINT; the shared fixture uses explicit
integer IDs to preserve the common customer model. Metadata assertions use SingleStore
SQL; they do not claim an extensible schema metadata reader implementation.
ODBC fixtures remain explicitly ignored pending #139 and ODBC is not advertised
as a passing manifest capability. New bulk ingestion and advanced DDL are outside
this issue. The Dev Image is for functional QA, not production or benchmarking.

Results: `.test-results/singlestore/<framework>/`. Bundles:
`Providers/MySql/DubUrl.Provider.SingleStore.QA/bin/qa/singlestore/<version>/<framework>/portable/`.
Each bundle includes the runnable adapter, driver, test host, manifest, Compose
definition, SQL fixtures and this guide. To run an adapter against a separately
prepared disposable fixture, set `DUBURL_SINGLESTORE_QA_URL` first.

| CI dimension | Repository evidence and decision |
| --- | --- |
| Frameworks | The shared build supports net8.0/net9.0/net10.0; each gets its matching pinned SDK and separate job. |
| Runner | Database manifest requires Linux amd64; use Ubuntu 24.04 x64. No additional native runner matrix is introduced. |
| Deliverable | Existing portable, framework-dependent QA bundle and repository version; no new NuGet package, RID, AOT or executable distribution. |
| Triggers and permissions | Pull requests, pushes to main and manual runs; read-only repository permission with no license secrets or publishing. |
| Verification | Require executed tests in complete and Schema TRX reports, and upload per-framework result/bundle artifacts. |

Reference: [SingleStore Dev Image documentation](https://github.com/singlestore-labs/singlestoredb-dev-image)
describes license-free CI use, partition limits, CPU requirements and unsupported
production/benchmark workloads.
