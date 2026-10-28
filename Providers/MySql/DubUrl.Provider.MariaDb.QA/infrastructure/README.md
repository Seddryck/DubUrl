# MariaDB QA infrastructure

Run `./QA/DubUrl.QA/MariaDb/deploy-mariadb-test-env.ps1` from the repository root
with PowerShell 7, Docker and the .NET 8/9/10 SDKs listed in the script. The Compose
file pins a verified Linux amd64 MariaDB image. Each run generates credentials,
waits for an authenticated SQL query, initializes deterministic fixtures, and
removes its container/volumes in finally. No database license is required.

Full runs require the MariaDB ODBC driver. Linux CI installs odbc-mariadb and
unixODBC and supplies a process-local driver registry alias. For a native-only
local run use `-testFilter 'TestCategory!=ODBC'`; full CI requires ODBC tests to
execute. The suite asserts MariaDB server identity, so MySQL cannot silently
substitute for MariaDB coverage. MySQL's separate suite remains unchanged.

Use `-frameworks` for a focused run, `-sdkVersions` for installed SDK versions,
`-port` for a busy port 3306, and `-readinessTimeoutSeconds` for slower hosts.
Results are under `.test-results/mariadb/`; bundles under
`bin/qa/mariadb/<version>/<framework>/portable/` in this project.

See [all QA commands and CI decisions](https://github.com/Seddryck/DubUrl/blob/main/docs/provider-qa-coverage.md).
