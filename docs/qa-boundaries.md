---
layout: doc
title: Provider QA boundaries
permalink: /qa-boundaries/
---

The shared QA projects are grouped in the solution's `QA` folder and physically located under `QA/`:
`QA/DubUrl.QA`, `QA/DubUrl.Provider.Testing`, and `QA/DubUrl.ProviderTesting`.
Standalone schema deployment and discovery fixtures, together with `DubUrl.Schema.Testing.Contracts`, live in [DubUrl.Schema](https://github.com/Seddryck/DubUrl.Schema/tree/main/QA).
Provider-specific live suites remain alongside their provider families under `Providers/<Family>/`.

Provider QA follows the same ownership boundary as provider production code. A provider named `XYZ` uses these projects:

- `DubUrl.Provider.XYZ` contains production provider registration, mapping, rewriting, dialect, and parameter behavior.
- `DubUrl.Provider.XYZ.Testing` contains fast unit tests for that production project. It may reference test frameworks and test helpers, but production projects must never reference it.
- `DubUrl.Provider.XYZ.QA` contains live tests, the database driver, and deployment assets. It consumes reusable provider contracts from `DubUrl.ProviderTesting`. Schema deployment, rendering, and discovery integration tests belong in the matching `DubUrl.Schema.XYZ.QA` suite in the Schema repository, including scenarios using encrypted provider connections. Provider QA must not reference Schema packages.

Provider QA projects reference the provider-neutral Core project, published as `DubUrl`, and their own provider implementations explicitly. Shared QA contracts do not pull in an all-provider aggregate.

## Dependency convention

```text
DubUrl -> DubUrl.Provider.XYZ -> provider unit and real-database QA
DubUrl -> DubUrl.Schema (separate repository) -> schema contracts and database QA
```


QA-only packages such as NUnit, Dapper, DbReader, database drivers, test hosts, and adapters belong in testing or QA projects. They must not be added to `DubUrl.Provider.XYZ` to support tests.

## Running a provider suite

Run every capability owned by a provider:

```powershell
dotnet test Providers/PostgreSql/DubUrl.Provider.PostgreSql.QA/DubUrl.Provider.PostgreSql.QA.csproj
```

Run standalone schema QA from the Schema repository:

```powershell
./QA/run.ps1 -Database PostgreSql -Framework net10.0
```

PostgreSQL accepts `DUBURL_POSTGRESQL_QA_URL` for the DubUrl connection URL and `DUBURL_POSTGRESQL_QA_CONNECTION_STRING` for the driver connection string. Defaults match the bundled Compose environment.

Oracle provisions its own disposable Oracle Free Lite container and dedicated test
schema with generated credentials. Run the same entry point locally and in CI:

```powershell
./QA/DubUrl.QA/Oracle/deploy-oracle-test-env.ps1
```

See [Oracle QA infrastructure](https://github.com/Seddryck/DubUrl/tree/main/Providers/Oracle/DubUrl.Provider.Oracle.QA/infrastructure)
for prerequisites, focused framework selection, image pinning and coverage limits.

## QA bundles

Every provider QA project imports `build/ProviderQaBundle.targets`, owns a validated `qa-manifest.json`, and declares its infrastructure files with `QaInfrastructure`. Create a complete versioned bundle for one target framework with:

```powershell
dotnet msbuild Providers/PostgreSql/DubUrl.Provider.PostgreSql.QA/DubUrl.Provider.PostgreSql.QA.csproj -t:QaBundle -p:TargetFramework=net8.0
```

The output under `bin/qa/<provider>/<version>/<framework>/<rid>` contains:

- `adapter/`: the test assembly, `.deps.json`, `.runtimeconfig.json`, test host/adapter files, and managed/native dependencies;
- `infrastructure/`: database initialization and reset assets;
- `qa-manifest.json`: provider, version, contract version, target frameworks, operating systems/RID support, and capabilities.

The manifest is checked by the provider suite, so malformed or incomplete metadata fails QA.

## GitHub Actions

Provider QA runs independently from the unit-test and release workflow. Each provider workflow calls its existing deployment entry point under `DubUrl.QA`, runs the provider-owned suite on .NET 8, 9, and 10, and uploads its test results and runnable QA bundle. Power BI Desktop remains manual because it requires licensed external software. SingleStore uses its license-free Dev Image and runs automatically on pull requests and pushes to main.

Run SingleStore locally with `./QA/DubUrl.QA/SingleStore/deploy-singlestore-test-env.ps1`.
It uses the same harness as CI, generated test credentials and a disposable
two-partition database. See its provider-owned `infrastructure/README.md` for
the image pin, CPU requirements and explicit ODBC exclusions.

## Multi-provider composition QA

`DubUrl.QA` remains as the shared deployment harness and multi-provider composition suite. Provider-owned projects cover provider capabilities, while the composition suite retains only composition concerns:

- explicitly referenced provider implementations can be discovered automatically;
- aliases do not conflict;
- multiple providers coexist in one catalog;
- dependency injection builds the expected catalog;
- resolving one provider does not change another provider's resolution.

Provider-owned connectivity, querying, schema, driver, and deployment scenarios must not be copied back into the composition suite. SQLite and PostgreSQL ADO.NET live coverage is now owned by their provider QA projects; their legacy composition fixtures were removed.

## Schema overlap audit

All 22 provider QA projects and their shared provider contracts were checked for Schema references and schema deployment/discovery assertions. DuckDB was the only provider QA project using `DubUrl.Schema`; its encrypted `SchemaInspector` scenario now belongs to `DubUrl.Schema.DuckDb.QA`.

- DuckDB retains encryption, native ADO.NET `GetSchema`, transaction, and typed bulk-copy checks. These verify the provider connection independently of Schema.
- SingleStore retains native auto-increment type promotion and fixture column/partition checks. Schema QA owns portable identity deployment and discovery.
- Db2 retains native typed-value and catalog checks; there is no matching Db2 Schema QA suite.
- MsAccess, PowerBiDesktop, SsasMultidim, SsasTabular, Drill, MsExcel, FirebirdSql, MariaDb, MySql, Oracle, CockroachDb, CrateDb, PostgreSql, QuestDb, Timescale, Sqlite, MsSqlServer, Text, and Trino contain no Schema integration assertions to move.

Creating tables as test setup, querying native catalogs, and testing ADO.NET metadata do not by themselves exercise the Schema library. Keep these provider tests separate from Schema's model, rendering, deployment, and discovery contracts.
