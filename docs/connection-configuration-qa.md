# Shared connection configuration validation

Validated on Windows on 2026-10-30 with the installed .NET 11 preview SDK targeting the repository's
supported .NET 8, 9 and 10 frameworks. All connections in the mapping tests remain closed.

| Project / focused selection | net8.0 | net9.0 | net10.0 |
|---|---:|---:|---:|
| DubUrl.Core.Testing | 173 passed | 173 passed | 173 passed |
| DubUrl.Cli.Testing | 106 passed | 106 passed | 106 passed |

Total: **837 passed test executions**, with no failed selected non-explicit tests.

Core filter (all non-explicit cases in these classes):

- `SharedConnectionConfigurationTests`
- `ConnectionConfigurationMappingTest`
- `OptionalConnectionOptionsTest`
- `CertificateOptionsTests`
- `ClientCertificateAuthenticationTests`
- `OperatingSystemAuthenticationTests`
- `PasswordAuthenticationTests`
- `TeradataRewriterTest`

CLI filter (all non-explicit cases in these classes):

- `SharedConnectionOptionsTests`
- `CliCertificateTests`
- `CliAuthenticationTests`
- `ConnectionCheckLifecycleTests`
- `ReadOnlyCommandTests`
- `SchemaInspectCommandTests`

Each project was tested with `dotnet test <project> -f <framework> --filter <selection>`, with the
class predicates combined using `FullyQualifiedName~<class>|...`. Focused tests ran before the
solution build. `dotnet build DubUrl.sln` and the final `dotnet build DubUrl.sln --no-restore` both
succeeded across all declared target frameworks. The final incremental build reported six warnings
and zero errors. `git diff --check` passed.

The tests cover native builders and command defaults, application metadata/query-band preservation,
verified-TLS settings and conflicts, custom/system CA selection, timeout precision and precedence,
pool-size/lifetime mappings, authentication composition/certificate callbacks, initialization on
reopen, initialization-failure disposal, caller cancellation, native timeout propagation, CLI
selectors/help/duration validation, safe diagnostics and dry runs. Schema inspection regressions
exercise local SQLite; they do not validate the new options against a remote server.

Not run:

- The complete repository test suite and unrelated QA projects.
- Linux or macOS builds/tests or installed ODBC/OLE DB driver execution.
- Explicit/live certificate, OS-identity, OAuth, database-provider and pool QA fixtures. No
  `DUBURL_*` fixture environment variables were configured for this run.
- Real trusted/untrusted-CA TLS handshakes, wrong-hostname/no-TLS failures, observable remote
  application/session metadata, daylight-saving server behavior, controlled slow queries,
  physical-session reuse/isolation, maximum-pool-size waiting, rotation or server-side cancellation.

Native mapping tests and fake lifecycle tests do **not** prove these live behaviors. The documented
support table distinguishes the implemented route contracts, external prerequisites and rejected
options; live server verification remains a separate fixture requirement.

## CI follow-up

The first Linux core run found two repeated-configuration failures in
`OperatingSystemIdentityMappingTests.MappingsConfigureClosedNativeConnections`: psqlODBC identity
configuration appended `require_auth` more than once. Configuration now preserves the existing TLS
trust options and adds the authentication restriction only once. A new regression case combines
custom-CA TLS with repeated identity configuration on the same closed native connection.

After that fix and the quality-gate refactoring, Windows validation reran these focused selections
on each of net8.0, net9.0 and net10.0:

- Core: `SharedConnectionConfigurationTests`, `OptionalConnectionOptionsTest`,
  `ConnectionConfigurationMappingTest`, `OperatingSystemIdentityMappingTests`,
  `CertificateOptionsTests`: **163 passed per framework**.
- CLI: `SharedConnectionOptionsTests`, `CliCertificateTests`, `CliAuthenticationTests`,
  `ConnectionCheckLifecycleTests`: **77 passed per framework**.

Total: **720 passed executions**. The full solution build and `git diff --check` passed again.
The complete test suites and live external fixtures were not run locally for this follow-up;
GitHub's Linux and provider workflows validate those separately.
