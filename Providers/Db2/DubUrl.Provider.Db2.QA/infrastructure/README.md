# Db2 QA infrastructure

Run `./QA/DubUrl.QA/Db2/deploy-db2-test-env.ps1` from the repository root with
PowerShell 7, Docker and the .NET 8/9/10 SDKs listed in the script. Use `-frameworks`
for a focused run, `-sdkVersions` for installed SDK versions, and `-port` to avoid
50000 conflicts. A disposable privileged Linux amd64 container accepts the IBM
Community license, generates instance credentials, waits for setup completion and
SQL readiness, initializes fixtures, and is removed in finally. No paid server
license is required. Allow at least 4 GB Docker memory and 10 GB free disk.

The Compose file pins the verified server manifest. IBM's .NET CLI driver is
OS-specific: bundles use `linux-x64` or `win-x64`, not a cross-platform native
binary. On Linux, add the adapter's `clidriver/lib` directory to LD_LIBRARY_PATH
and set DB2_CLI_DRIVER_INSTALL_PATH to its `clidriver` directory when running an
exported bundle. The deployment script configures this automatically. Linux CI
installs libxml2, libpam and libaio; local hosts must provide those dependencies.

Coverage includes connection/authentication, queries, parameters, date/time/null
mappings, basic DDL and dictionary metadata. The shared ANSI interval contract is
explicitly skipped. Results are under `.test-results/db2/`; bundles under
`bin/qa/db2/<version>/<framework>/<os>-x64/` in this project.

See [all QA commands and CI decisions](https://github.com/Seddryck/DubUrl/blob/main/docs/provider-qa-coverage.md).
