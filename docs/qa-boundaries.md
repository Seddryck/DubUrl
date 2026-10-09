# Provider QA boundaries

Provider QA follows the same ownership boundary as provider production code. A provider named `XYZ` uses these projects:

- `DubUrl.Providers.XYZ` contains production provider registration, mapping, rewriting, dialect, and parameter behavior.
- `DubUrl.Providers.XYZ.Testing` contains fast unit tests for that production project. It may reference test frameworks and test helpers, but production projects must never reference it.
- `DubUrl.Providers.XYZ.QA` contains live tests, the database driver, and deployment assets. It consumes reusable contracts from `DubUrl.ProviderTesting` and `DubUrl.Schema.Testing.Contracts`.

Until the production split in #1573 is complete, pilot QA projects reference `DubUrl.Core` directly. This is a migration seam, not a reversal of the dependency direction.

## Dependency convention

```text
DubUrl.Core                    DubUrl.Schema
    ^                               ^
    |                               |
DubUrl.Providers.XYZ          DubUrl.Schema.Testing.Contracts
    ^                               ^
    |                               |
DubUrl.Providers.XYZ.Testing   DubUrl.Providers.XYZ.QA
                                    |
DubUrl.ProviderTesting <------------+
```

QA-only packages such as NUnit, Dapper, DbReader, database drivers, test hosts, and adapters belong in testing or QA projects. They must not be added to `DubUrl.Providers.XYZ` to support tests.

## Running a provider suite

Run every capability owned by a provider:

```powershell
dotnet test DubUrl.Providers.PostgreSql.QA
```

Run only its Schema contract:

```powershell
dotnet test DubUrl.Providers.PostgreSql.QA --filter "TestCategory=Schema"
```

PostgreSQL accepts `DUBURL_POSTGRESQL_QA_URL` for the DubUrl connection URL and `DUBURL_POSTGRESQL_QA_CONNECTION_STRING` for the driver connection string. Defaults match the bundled Compose environment.

## QA bundles

Every provider QA project imports `build/ProviderQaBundle.targets`, owns a validated `qa-manifest.json`, and declares its infrastructure files with `QaInfrastructure`. Create a complete versioned bundle for one target framework with:

```powershell
dotnet msbuild DubUrl.Providers.PostgreSql.QA -t:QaBundle -p:TargetFramework=net8.0
```

The output under `bin/qa/<provider>/<version>/<framework>/<rid>` contains:

- `adapter/`: the test assembly, `.deps.json`, `.runtimeconfig.json`, test host/adapter files, and managed/native dependencies;
- `infrastructure/`: database initialization and reset assets;
- `qa-manifest.json`: provider, version, contract version, target frameworks, operating systems/RID support, and capabilities.

The manifest is checked by the provider suite, so malformed or incomplete metadata fails QA.

## GitHub Actions

Provider QA runs independently from the unit-test and release workflow:

- `.github/workflows/qa-postgresql.yml` starts the bundled Docker Compose environment, runs the complete PostgreSQL suite and its Schema category on .NET 8, 9, and 10, then uploads each runnable QA bundle.
- `.github/workflows/qa-sqlite.yml` runs the complete self-contained SQLite suite and its Schema category on .NET 8, 9, and 10, then uploads each runnable QA bundle.

Both workflows call the existing provider deployment entry points under `DubUrl.QA`. Those scripts preserve their AppVeyor/local behavior and select the provider-owned projects only when `GITHUB_ACTIONS=true`.

## Aggregate compatibility QA

`DubUrl.QA` remains during the migration for providers that do not yet own a QA project. Once each provider moves, the aggregate suite retains only composition concerns:

- all advertised built-ins can be discovered by the compatibility package;
- aliases do not conflict;
- multiple providers coexist in one catalog;
- dependency injection builds the expected catalog;
- resolving one provider does not change another provider's resolution.

Provider-owned connectivity, querying, schema, driver, and deployment scenarios must not be copied back into the aggregate suite. SQLite and PostgreSQL ADO.NET live coverage is now owned by their provider QA projects; their legacy aggregate fixtures were removed.
