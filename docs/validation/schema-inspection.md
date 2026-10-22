# Schema inspection validation

Validated on Windows on 2026-10-22 from the dedicated feat/schema-inspection worktree, created from origin/main at a731ced.

## Build

- dotnet build DubUrl.sln --no-restore --nologo -v quiet -m:4 passed for net8.0, net9.0 and net10.0.
- An earlier full restore/build used -p:NuGetAudit=false because the vulnerability endpoint was unavailable in the sandbox.
- Existing NUnit1034 warnings in MySQL tests and CA1416 warnings in OleDb tests remain; no build errors.
- dotnet format whitespace was scoped to changed C# files. git diff --check passed.

## Tests run

Focused new tests were run before broader affected-project regression checks. The final matrix used dotnet test <project>/<project>.csproj -f <framework> --no-build --no-restore with the existing NUnit adapter. Each matrix entry below passed on net8.0, net9.0 and net10.0.

| Project | Selection | Passed per framework |
|---|---|---:|
| DubUrl.Core.Testing | FullyQualifiedName~Discovery OR FullyQualifiedName~ProviderAssemblyBoundaryTest | 125 |
| DubUrl.Schema.Documents.Testing | Entire project | 127 |
| DubUrl.Schema.Testing | Entire project | 95 |
| DubUrl.Application.Testing | Entire project | 40 |
| DubUrl.Cli.Testing | Entire project | 49 |
| DubUrl.Worker.Testing | Entire project | 27 |
| **Total** | **Three frameworks** | **1,389 executions** |

The Core filter was passed as --filter "FullyQualifiedName~Discovery|FullyQualifiedName~ProviderAssemblyBoundaryTest". It includes provider registration/dependency tests, custom discovery contracts, ownership/disposal/cancellation and real embedded DuckDB catalog/round-trip fixtures. Documents cover published JSON Schema validation, JSON/YAML round trips, generation, native types/scopes, foreign keys, membership/regex checks, binary/temporal values, cultures and escaping.

Additionally ran Providers/Sqlite/DubUrl.Provider.Sqlite.QA with --filter FullyQualifiedName~SqliteDiscoveryTests on net10.0: one live in-memory SQLite rich-metadata fixture passed. It covers literal defaults, declared numeric facets, generated-expression diagnostics, ordered ordinary indexes, unsupported expression/partial indexes, and portable/arbitrary view definitions.

## Skipped and omitted

- Invoked PostgreSqlDiscoveryTests on net10.0: two fixtures compiled and skipped because DUBURL_POSTGRESQL_DISCOVERY_QA_CONNECTION_STRING was not configured.
- Invoked SqlServerDiscoveryTests on net10.0: two fixtures compiled and skipped because DUBURL_SQLSERVER_DISCOVERY_QA_CONNECTION_STRING was not configured.
- Docker's daemon was unavailable. No live PostgreSQL or SQL Server metadata behavior, server version, catalog permissions, or DDL round trip is claimed verified.
- Provider QA beyond the SQLite discovery fixture and the two explicitly skipped provider discovery classes was not run.
- Provider QA was not run on net8.0/net9.0.
- The full repository test suite and unrelated Core tests, BulkCopy, Extensions, Adomd and OleDb tests were not run.
- Linux/macOS, connector archive packaging, release/publishing, HTTP inspection (#1601), and shared release qualification (#1579) were not run or changed.

## Known model boundaries

Named table unique constraints, arbitrary views/SQL expressions, specialized indexes, SQLite catalog gaps and unbounded SQL Server facets produce explicit incomplete results. Scoped native types remain scoped in the domain; v1 export rejects them because the current document format cannot encode their scope. DuckDB generation/index metadata is explicitly unsupported, and its round-trip fixture uses DDL supported by the existing renderer. These capability limits are documented in docs/_docs/schema-inspection.md.
