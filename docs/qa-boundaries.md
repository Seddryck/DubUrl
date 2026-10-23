---
layout: doc
title: Provider QA boundaries
permalink: /qa-boundaries/
---

The shared QA projects are grouped in the solution's `QA` folder and physically located under `QA/`:
`QA/DubUrl.QA`, `QA/DubUrl.Provider.Testing`, `QA/DubUrl.ProviderTesting`, and
`QA/DubUrl.Schema.Testing.Contracts`. Project, assembly, and namespace names are unchanged.
Provider-specific live suites remain alongside their provider families under `Providers/<Family>/`.

Provider QA follows the same ownership boundary as provider production code. A provider named `XYZ` uses these projects:

- `DubUrl.Provider.XYZ` contains production provider registration, mapping, rewriting, dialect, and parameter behavior.
- `DubUrl.Provider.XYZ.Testing` contains fast unit tests for that production project. It may reference test frameworks and test helpers, but production projects must never reference it.
- `DubUrl.Provider.XYZ.QA` contains live tests, the database driver, and deployment assets. It consumes reusable contracts from `DubUrl.ProviderTesting` and `DubUrl.Schema.Testing.Contracts`.

Provider QA projects reference the provider-neutral Core project, published as `DubUrl`, and their own provider implementations explicitly. Shared QA contracts do not pull in an all-provider aggregate.

## Dependency convention

```text
DubUrl                         DubUrl.Schema
    ^                               ^
    |                               |
DubUrl.Provider.XYZ          DubUrl.Schema.Testing.Contracts
    ^                               ^
    |                               |
DubUrl.Provider.XYZ.Testing   DubUrl.Provider.XYZ.QA
                                    |
DubUrl.ProviderTesting <------------+
```

QA-only packages such as NUnit, Dapper, DbReader, database drivers, test hosts, and adapters belong in testing or QA projects. They must not be added to `DubUrl.Provider.XYZ` to support tests.

## Running a provider suite

Run every capability owned by a provider:

```powershell
dotnet test Providers/PostgreSql/DubUrl.Provider.PostgreSql.QA/DubUrl.Provider.PostgreSql.QA.csproj
```

Run only its Schema contract:

```powershell
dotnet test Providers/PostgreSql/DubUrl.Provider.PostgreSql.QA/DubUrl.Provider.PostgreSql.QA.csproj --filter "TestCategory=Schema"
```

PostgreSQL accepts `DUBURL_POSTGRESQL_QA_URL` for the DubUrl connection URL and `DUBURL_POSTGRESQL_QA_CONNECTION_STRING` for the driver connection string. Defaults match the bundled Compose environment.

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

Provider QA runs independently from the unit-test and release workflow. Each provider workflow calls its existing deployment entry point under `DubUrl.QA`, runs the provider-owned suite on .NET 8, 9, and 10, and uploads its test results and runnable QA bundle. Power BI Desktop and SingleStore remain manual workflows because they require licensed external software.

## Multi-provider composition QA

`DubUrl.QA` remains as the shared deployment harness and multi-provider composition suite. Provider-owned projects cover provider capabilities, while the composition suite retains only composition concerns:

- explicitly referenced provider implementations can be discovered automatically;
- aliases do not conflict;
- multiple providers coexist in one catalog;
- dependency injection builds the expected catalog;
- resolving one provider does not change another provider's resolution.

Provider-owned connectivity, querying, schema, driver, and deployment scenarios must not be copied back into the composition suite. SQLite and PostgreSQL ADO.NET live coverage is now owned by their provider QA projects; their legacy composition fixtures were removed.
