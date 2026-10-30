---
name: add-database-provider
description: Add support for a database engine, an alternative ADO.NET provider, or an ODBC, OLE DB, or ADBC driver in DubUrl, including implementation, unit and integration tests, live QA, CI, connector distribution, and documentation. Use for extending database connectivity; not for merely installing an existing connector.
---

# Add a database or provider

Deliver support that works through the public connection APIs and can be discovered, distributed, tested, and documented. Follow the repository's `AGENTS.md` for worktrees, branches, issues, validation, and pull requests. This skill does not authorize installing machine-wide drivers or accessing external databases.

## Establish the scope

Inspect the current checkout before choosing an example. Start with `docs/_docs/provider-assembly-boundaries.md`, `docs/_docs/provider-modules.md`, and `connectors/connector-matrix.json`. Use an existing provider with the same connection technology and similar database semantics as the implementation reference.

Distinguish these requests:

| Change | Expected work |
| --- | --- |
| New database engine | Database metadata and aliases; dialect and SQL behavior; mapper and rewriter; conversion; connector selection; tests, QA, and docs. Reuse an existing family when the engine genuinely shares its implementation. |
| Alternative managed provider for an existing engine | Reuse database metadata and dialect; add driver-specific mapping, rewriting, conversion, parameter behavior, and packaging. Preserve existing default aliases unless changing defaults is requested. |
| New ODBC/OLE DB transport for an engine | Reuse engine SQL behavior; add wrapper mapping, discovery and ranking, transport-specific rewriting/conversion, platform prerequisites, connector selection, and transport QA. |
| Another installed-driver name/version | Extend the locator's matching/ranking and tests. Add a mapper or connector only if the connection contract or independently selectable technology changes. |
| New ADBC implementation | Reuse shared Arrow/ADBC infrastructure; put the engine factory and native-library discovery in its provider project. Account for external native-library prerequisites and supported platforms. |

Before editing, record the database/family, driver invariant and assembly, URL aliases, default provider selection, supported frameworks/platforms, external prerequisites, and intended capabilities. Mark checklist items implemented, reused, unsupported, or out of scope with a reason. Do not turn every conditional capability into a requirement to implement it.

For new dependencies or uncertain driver behavior, consult the publisher's current documentation for connection keywords, factory identity, parameter conventions, native runtime support, licensing, and redistribution. Do not infer those contracts from a different driver for the same engine.

## Keep implementation ownership intact

- Put provider and QA projects under `Providers/<Family>/<Project>/`. Match namespaces to the project name and physical subfolder. Import `Providers/Provider.Build.props` in provider implementations following nearby projects.
- Keep metadata in `Metadata/`, mappers in `Mapping/`, forward rewriting in `Rewriting/`, reverse conversion in `Conversion/`, SQL behavior in `Sql/`, and optional discovery/schema behavior in their provider folders. Keep sources and resources physically with their owner.
- `DubUrl.Core` owns reusable contracts and infrastructure. Adding a database should not require database-specific branches or declarations in Core, Application, CLI, Worker, or shared schema templates. If a missing shared extension point prevents support, identify and justify that infrastructure change explicitly.
- Family providers normally remain independent of concrete ADO.NET driver packages. Driver composition belongs in `Providers/Build/ProviderBinding.Build.csproj`; typed ADBC factories and optional bulk adapters follow their existing specialized project boundaries.
- Native and wrapper implementations have different owners: inspect `Providers/Odbc/DubUrl.Provider.Odbc`, `DubUrl.OleDb`, and `Providers/Adomd`/`DubUrl.Adomd` for the applicable split. For ADBC, compare `Providers/DuckDb/DubUrl.Provider.DuckDb.Adbc` and `DubUrl.Adbc`.
- Add new projects to `DubUrl.sln` and applicable solution filters. Inspect wildcard project/source inclusions before adding explicit references; do not recreate an all-provider aggregate or introduce a binding DLL per connector.

## Define metadata and registration

- For a new engine, declare `Database<TDialect>` metadata with its name, unique aliases, category/listing priority, and appropriate `Brand` metadata. Search existing aliases case-insensitively before choosing new ones.
- Declare the native `Mapper` or appropriate wrapper attributes with the exact provider invariant and parameter convention. Keep alternative/default mapping behavior deliberate; inspect `NativeMapperIntrospector`, `WrapperMapperIntrospector`, and `AttributeProviderModule` instead of assuming every mapper is selected automatically.
- Preserve the constructor contract used by `ProviderCatalogBuilder`: connection-string builder, dialect, and parametrizer; add the root path for file-based mappers and locator options where needed. Follow existing file-based and wrapper interfaces.
- Exercise attribute discovery, application dependency discovery, and installed-connector selection for the affected path. Metadata discovery must not depend on a running database or an installed native driver. Factories unavailable at runtime must produce the established skip/diagnostic behavior.
- Reuse `ProviderFactoriesRegistrator` factory discovery and explicit `IDubUrlProviderModule`/`ProviderRegistration` extension points. Cover duplicate aliases, case-insensitive resolution, explicit factory overrides, and installed-pack precedence where the new selection can affect them.

## Compile and reverse connection URLs

Implement the forward rewriter against the selected provider's connection-string builder. Decide and test:

- host and default port; database/catalog/schema path segments; local, relative, absolute, empty, and in-memory paths where applicable;
- omitted versus empty user information, percent-encoded values, and provider-specific authentication fields;
- option translation, escaping, defaults, precedence, and rejection of conflicting or unsupported values;
- wrapper scheme composition and explicit driver/provider selection.

Add the corresponding connection-string reverser using a neighboring `Conversion/` implementation. Cover equivalent connection-string keywords, supported scheme selection, file paths, and meaningful round trips. Compare parsed semantics rather than requiring identical connection-string key ordering.

For ODBC, implement or extend `Drivers/Discovery/` in the ODBC provider: driver attributes, regex captures, architecture/encoding/version options, deterministic ranking, no-match diagnostics, and injected driver-list tests. Test realistic Windows and Unix driver names only where supported. For OLE DB, follow `DubUrl.OleDb/Providers/` and its provider-locator tests. For ADBC, test library/manifest lookup, ABI/load failures, handle lifetime, and a useful missing-library diagnostic without silently downloading a driver.

## Verify SQL and optional capabilities

Reuse an existing dialect only when its behavior fits the engine. For a new or changed dialect, check identifier quoting and qualification, literals, logical/native type mappings, SQL functions, return-value casters, and template rendering. Parameter syntax belongs to the selected provider/transport: verify named or positional placeholders, repeated parameters, binding order, nulls, and type assignment. An engine's native provider and its ODBC driver may differ.

Assess each capability separately:

| Capability | Integration and checks |
| --- | --- |
| Schema generation | Provider-owned `Schema/Generation/` renderer exposed by `IDialect.SchemaSqlRenderer`, plus truthful `DialectCapabilities` and native-type declarations. Check supported identity/generated columns, constraints, foreign keys, indexes, views, comments, and qualification. Capability flags describe support; they do not select SQL syntax. |
| Schema discovery | Provider-owned metadata reader registered through the existing discovery contract/attribute. Check schema/table/column/type/nullability/key metadata and naming/escaping using relevant shared discovery contracts. |
| Connection configuration | Declare supported `ConnectionConfiguration` keywords and conversions for application name, pooling, sizes, and timeouts. Verify unsupported settings fail through existing strict configuration behavior. |
| Read-only connections | Use the existing capability/attribute and driver-specific mechanism. Verify actual enforcement and conflict diagnostics; do not treat a read-only hint as enforcement without evidence. |
| Authentication | Check username/password, file secrets, OS identity, token/OAuth, and client certificates individually. Inspect `DubUrl.Core/Authentication`, driver adapters in `DubUrl.Authentication.*`, and authentication QA contracts. Reuse supported strategies; explicitly document unsupported ones. Verify credential conflicts, redaction, cancellation, refresh/pool lifecycle, and certificate validation when applicable. Do not copy scheme-specific authentication rules from another engine. |
| Bulk copy | Opt in on the provider registration/mapper, never on the dialect. Use an optional typed adapter project and lazy activation following DuckDB/SqlClient. Include it only in the matching connector. Test disposal, null/type conversion, missing dependencies, and absence on unsupported transports. |

Do not add CLI switches or hard-coded host lists just to expose a database. Verify CLI/Worker/public API behavior through existing catalogs and services; change shared behavior only when the requested support requires it.

## Make the connector distributable

1. Add or update `connectors/connector-matrix.json`: technology key/name and optional assembly override, database selector, family, unique database/driver ID, status, default installation aliases, supported RIDs, and prerequisites. A connector installation alias is distinct from a URL scheme. Avoid stealing an existing default when adding an alternative driver.
2. Update conditional family/technology project and package references in `Providers/Build/ProviderBinding.Build.csproj`. Keep versions consistent with test and adapter dependencies, including TFM-specific packages and native assets. Check license/redistribution requirements and `connectors/THIRD-PARTY-NOTICES.md`.
3. Let `connectors/Catalog` derive mappings, schemes, dialects, and implementation assemblies from provider attributes. The matrix selects packaging; do not duplicate implementation types or scheme lists there. Inspect canonical grouping: compatible selections sharing implementation/driver/runtime use one archive with installation aliases.
4. Verify generated definitions describe every selected database mapping and identify real implementation and driver assemblies. Check the provider-specific `.deps.json`, dependency closure, external prerequisites, optional adapters, native assets, and host-shared assembly exclusions. `ConnectorDependencies.dll` is a temporary collector and must not ship.
5. Verify the generated Application catalog and CLI connector listing expose the new selection without loading its implementation. Exercise installation/activation and connection resolution using a disposable, task-local provider store when practical.

Use `connectors/Build-ConnectorArtifacts.ps1`, `Test-ConnectorArtifacts.ps1`, `Test-ConnectorArchiveSet.ps1`, and `Test-ConnectorDependencies.ps1` as applicable; read their parameters first. Artifact building currently processes all ready selections, so prefer focused provider/catalog checks first and report when full archive validation is deferred. Use task-local output paths; the build script recreates its output directory.

## Add and run the tests

- Colocate provider unit tests under the owning project's `Tests/`. `Provider.Build.props` excludes them from shipping assemblies; `DubUrl.Core.Testing` includes provider test sources. Shared ADBC and OLE DB infrastructure also have dedicated runners (`DubUrl.Adbc.Testing`, `DubUrl.OleDb.Testing`); check project inclusions to choose the actual runner.
- Cover the changed metadata/discovery, forward rewriting, reverse conversion, locator matching/ranking, SQL behavior, and capability contracts. Run relevant `QA/DubUrl.Provider.Testing` boundary, binding, and default-discovery tests when adding a family, technology, or packaging selection.

Use this inventory to decide which existing suites to extend. Add observable assertions for the new provider, not only updates to expected counts or names.

| Area | Test location and required evidence |
| --- | --- |
| Metadata and registration | Provider `Tests/` and `DubUrl.Core.Testing/Mapping/`: engine aliases, invariant name, correct dialect/parametrizer, constructor discovery, alternative selection, missing factory, and duplicate-alias handling. |
| Forward and reverse conversion | Provider `Tests/Rewriting/` and `Tests/Conversion/`: defaults, omitted/empty/encoded values, credentials/options, file paths, malformed input, and semantic round trips for the new provider. |
| Driver discovery | Owning ODBC provider tests, `DubUrl.OleDb.Testing`, or `DubUrl.Adbc.Testing` as appropriate: accepted and rejected names, architecture/version/encoding preference, deterministic ties, explicit override, and no-driver errors using injected inventories. |
| Query execution and SQL | Provider `Tests/Sql/` and relevant Core tests: quoting, literals, types/functions/casters, bound parameter values/types/order, repeated parameters, and nulls. Assert executed command content and bindings rather than an unrelated mocked return value. |
| Schema | `DubUrl.Schema.Testing/ProviderSchemaSqlRendererTests.cs` and relevant renderer/discovery suites: supported DDL, unsupported-operation diagnostics, name qualification, native types, and provider capability declarations. |
| Configuration/authentication/bulk | Existing suites in `DubUrl.Core.Testing`, driver authentication projects, and `DubUrl.BulkCopy.Testing`: supported behavior and negative cases for the capabilities actually introduced. |
| Boundaries and host integration | `QA/DubUrl.Provider.Testing/ProviderSourceBoundaryTest.cs`, `ProviderBindingTest.cs`, `DefaultProviderDiscoveryTest.cs`; `DubUrl.Application.Testing/BundledProviderCatalogTests.cs` and `ConnectorServiceTests.cs`; affected CLI/Worker tests. Verify the new selection is reachable without eagerly loading unrelated providers. |

Choose filters from actual test class names and run the affected project first. For example, after substituting the real provider test names and a supported installed framework:

```powershell
dotnet test DubUrl.Core.Testing/DubUrl.Core.Testing.csproj --configuration Release --framework net10.0 --filter 'FullyQualifiedName~ExampleRewriterTest|FullyQualifiedName~ExampleConversionTest'
dotnet test QA/DubUrl.Provider.Testing/DubUrl.Provider.Testing.csproj --configuration Release --framework net10.0 --filter 'FullyQualifiedName~ProviderSourceBoundaryTest|FullyQualifiedName~ProviderBindingTest|FullyQualifiedName~DefaultProviderDiscoveryTest|FullyQualifiedName~CliQaCoverageTests'
```

These are command shapes, not evidence that tests ran. Replace example filters, inspect the runner's executed count, and retain results. Add a regression case for any defect discovered while onboarding; do not broaden to unrelated suites without a reason.

## Add reproducible live QA and CI coverage

- Add or extend `Providers/<Family>/DubUrl.Provider.<Database>.QA/` with the appropriate shared `ProviderContract`, `OdbcContract`, `OleDbContract`, or `AdomdContract` from `QA/DubUrl.ProviderTesting`. Cover connection, query, parameters, templates, return conversion, and relevant authentication/schema/CLI contracts. Demonstrate the intended engine and transport rather than testing only a compatible substitute.
- Supply a validated `qa-manifest.json`, matching capability categories and supported TFMs/OSes/RIDs, provider-owned `infrastructure/`, and the `build/ProviderQaBundle.targets` import. Copy the required manifest/infrastructure into the bundle. State unsupported features and licensed-driver gaps explicitly; avoid disabling inherited assertions just to obtain a green run.
- Extend the appropriate `QA/DubUrl.QA/<Database>/` deployment script and `.github/workflows/qa-*.yml`. Reuse readiness, isolation, cleanup, filtering, and TRX checks in `Run-ProviderQaSuite.ps1`. Required categories must execute tests: zero tests, missing results, and unexpected skips are failures. Keep secrets out of logs and bundles.
- Select CI frameworks and OS runners from actual support and native prerequisites. Follow the repository CI skill when substantially changing delivery workflows. Local QA must not install/register machine-wide prerequisites without permission.

For each supported database/driver pair, include these QA deliverables:

1. A fixture using that actual database and driver, with deterministic seed data and expected results. Supply named/positional query examples, typed/null/date/time return cases, and supported template/ORM paths through the shared contracts. Record reasons for each `Supports*` override.
2. A CLI connection-check path inherited from `CliConnectionCheckContract`, plus the applicable password, OS identity, token/OAuth, or certificate contracts. Verify unsupported authentication produces the expected diagnostic. Inspect `QA/DubUrl.Provider.Testing/CliQaCoverageTests.cs`: its provider-suite inventory currently has an explicit count that must be reviewed when adding a QA project, while preserving its transport-contract checks.
3. Infrastructure under the QA project: Compose/service configuration or local fixture, initialization scripts/data, bounded readiness, isolated database/schema names and ports, and cleanup after success, failure, or cancellation. Keep credentials and generated certificate/private-key material out of tracked fixtures and artifacts.
4. A deployment entry point under `QA/DubUrl.QA/<Database>/` that selects the new project and test categories. Ensure new QA dependencies, native libraries, and infrastructure are included in the runnable bundle rather than relying on the developer checkout or an unrelated provider's output.
5. A CI job for every claimed transport/platform combination, with framework/category filters, prerequisite checks, TRX verification, and result/bundle upload. Separate optional licensed-driver gaps from required categories; do not allow a required category to pass entirely skipped.
6. A recorded local smoke run when prerequisites exist, including the exact command, framework, database/driver versions, categories, and executed/passed/skipped counts. If prerequisites are absent, still complete fixtures, scripts, CI wiring, and documentation; explicitly report the live run as not performed.

Use `docs/qa-boundaries.md` and `docs/provider-qa-coverage.md` for the current split between mocked tests and live suites. Read `docs/provider-authentication-qa.md`, `docs/cli-authentication-qa.md`, `docs/token-authentication-qa.md`, `docs/client-certificate-qa.md`, or `docs/operating-system-identity-qa.md` only for the authentication modes being added.

## Update user and maintainer documentation

Treat docs as part of database/provider support, not an optional follow-up. Update applicable rows below; mark the others reused or not applicable in the completion report.

| Documentation | What to add or update |
| --- | --- |
| Provider reference | `docs/_docs/native-ado-net-providers.md`, `odbc-driver-locators.md`, `oledb-provider-locators.md`, `adbc-providers.md`, or `adomd-providers.md`: database aliases, selected driver/provider, discovery rules, supported platforms, and limitations. Generated tables also need regenerated data. |
| Installation and selection | `docs/_docs/installation-ado-net-provider.md`, `choose-provider.md`, and `provider-packs.md`: the real family package and driver dependencies or connector ID, default/alternative selection, native prerequisites, architecture restrictions, and licensing steps. Distinguish NuGet usage from installed connector usage. |
| Working examples | A minimal connection and query using real supported aliases, correct imports/packages, escaping, and disposal; include an explicit transport/alternative-provider example when applicable. Add file/in-memory forms only where supported and use placeholder credentials. |
| Feature support | Applicable schema, discovery, read-only, pooling/timeouts, bulk-copy, and authentication pages: supported behavior, required configuration, unsupported combinations, and expected diagnostics. Keep advertised support consistent with actual tests and QA. |
| QA instructions and coverage | `docs/provider-qa-coverage.md`, `docs/qa-boundaries.md`, and applicable authentication QA pages: exact local commands and filters, prerequisites, seeding/readiness/cleanup, platform/TFM coverage, output locations, and intentional gaps. |
| Branding and navigation | For a new brand, inspect `docs/_data/brand_colors.yml`, generated brand metadata, and database icon licensing/attribution. Add navigation links only when introducing a new page, following the existing frontmatter and navigation data conventions. |
| Overview | Update `README.md` or support summaries when they enumerate databases, providers, or capabilities affected by the addition. Avoid unrelated prose rewrites. |

Regenerate affected provider data with the root `generate-adonet-info.ps1`, `generate-odbc-driver-info.ps1`, `generate-oledb-provider-info.ps1`, or `generate-adomd-info.ps1` scripts as appropriate. Read each script's prerequisites and exit-code conventions first: the generators can return exit code 1 to signal changed data. `build/Import-DocumentationAssemblies.ps1` expects a built `QA/DubUrl.QA` output for net8.0; ensure the new assembly participates. Review generated diffs rather than hand-editing derived catalogs or copying stale output.

Check affected `docs/_data/natives.json`, `odbc.json`, `oledb.json`, and `adomd.json` for the expected entry and unchanged unrelated providers. Validate page frontmatter, local links, example imports, package names, aliases, and agreement with generated metadata. Use `build/verify-doc-examples.ps1` for examples it actually covers; extend its case selection when adding a relevant snippet rather than claiming it validates every Markdown code block. Build/preview affected documentation pages using the repository's existing site workflow when layout or generated tables change.

## Validate and hand off

Validate in increasing scope:

1. Run focused tests for the new mapper/rewriter/conversion/locator/dialect/capability using the actual owning test project and a narrow filter. Confirm the expected tests executed.
2. Build the solution per `AGENTS.md`, including supported TFM compilation; ensure new projects are included in the relevant solution/filter. Run affected provider source-boundary and catalog/connector checks.
3. Run relevant live QA with one framework/category first when prerequisites exist, then the required supported combinations in CI. Validate bundles and archives when distribution changes. Avoid an unrelated full test suite.
4. Review documentation, generated metadata, package inventory, and clean Git state. Complete the repository's commit/push/PR workflow and issue linkage.

Report the database and driver added, aliases/default selection, supported and unsupported capabilities/platforms, exact tests/builds/QA/artifact checks run, and checks not run with reasons. Compilation and mocked tests alone do not establish live driver compatibility.
