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
| DubUrl.Core.Testing | FullyQualifiedName~Discovery OR FullyQualifiedName~ProviderAssemblyBoundaryTest | 136 |
| DubUrl.Schema.Documents.Testing | Entire project | 127 |
| DubUrl.Schema.Testing | Entire project | 140 |
| DubUrl.Application.Testing | Entire project | 40 |
| DubUrl.Cli.Testing | Entire project | 49 |
| DubUrl.Worker.Testing | Entire project | 27 |
| **Total** | **Three frameworks** | **1,557 executions** |

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

Named table unique constraints, arbitrary views/SQL expressions, specialized indexes, SQLite catalog gaps and unbounded SQL Server facets produce explicit incomplete results. Scoped native types remain scoped in the domain; v1 export rejects them because the current document format cannot encode their scope. DuckDB supports dedicated sequence-backed identity, representable virtual generated expressions and ordinary/unique index column order. Shared/cycling/custom-bound sequences and unsupported generated/index expressions are diagnosed. DuckDB discards index ASC/DESC directions; discovery leaves them unspecified. Its foreign-key DDL round-trip limitation remains. These capability limits are documented in docs/_docs/schema-inspection.md.

## DuckDB follow-up validation

The same full solution build and six-project matrix above were rerun after adding DuckDB identity, generated-expression and ordinary-index discovery/rendering. The Core selection now has 136 tests per framework; the matrix totals 1,422 passing executions.

DuckDbDiscoveryTests contains 13 live DuckDB 1.5.6 cases per framework. They cover sequence seed and positive/negative increment, zero seed, read-only sequence counters during inspection, generated Length/Upper values, quoted schema/table/column/sequence/index names, compound unique index column order, stable JSON and SQL round trips, executable deployment and DropIfExists identity reset. They also reject shared/cycling/custom-bound sequences, expressions around nextval, arithmetic generated expressions and expression indexes. The catalog does not retain ASC/DESC declarations, and tests require unspecified discovered sort direction.

The existing SQLite discovery QA fixture was rerun on net10.0 and passed. SQL Server/PostgreSQL live fixtures were not rerun during this follow-up; their earlier four skips and missing QA connections remain as recorded above. Unrelated repository suites, other provider QA, Linux/macOS and release packaging remain omitted.

A temporary in-memory probe also verified that DuckDB 1.5.6 rejects native GENERATED ALWAYS AS IDENTITY, returns empty information_schema generation fields, includes generated expressions in column_default, and discards ASC/DESC from canonical index SQL. The production reader uses catalog data without evaluating those expressions or advancing sequences.

## Provider renderer refactor validation

After moving all engine-specific StandardSchemaSqlRenderer branches into provider assemblies, reran the
full solution build and the six-project, three-framework matrix above: 1,557 passing executions. First ran
ProviderSchemaSqlRendererTests and SchemaSqlRendererExtensionTests on net10.0: 52 passing tests.
The 45 new regression cases verify renderer assembly ownership and preserved generated-column, regex
and qualified-name SQL across nine dialects. Extension tests verify explicit delegation and that copied
capability metadata cannot implicitly select an engine renderer. Existing generation tests cover identity,
foreign-key placement, rejected qualifiers and index naming.

The live SQLite discovery fixture was rerun on net10.0 and passed. PostgreSQL/SQL Server live fixtures
were not rerun; their earlier missing QA connections and four skips remain as recorded above. All other
omissions above remain applicable. Architecture documentation records the capability API migration.
