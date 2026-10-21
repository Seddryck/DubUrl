# Connector publication matrix

`connector-matrix.json` is the authoritative inventory and build definition for DubUrl connector packs. Rows describe database installation choices. Choices sharing implementation assemblies, driver and runtime compatibility are grouped into one archive/catalog entry, validated for archive integrity, loading, activation and removal.

All 32 database/technology installation names remain available through 24 archives. Managed connectors carry their ADO.NET dependency closure. DuckDB and SQLite additionally carry the native assets published by their upstream binding packages, with explicit runtime compatibility metadata.

ODBC and OLE DB connectors carry the .NET bridge but do not redistribute database-vendor drivers or providers. Their archives include an external-prerequisite notice, and the matching machine-level driver must be installed separately. Access and Excel connectors are restricted to supported Windows runtime identifiers.

Connector IDs identify the database/driver combination, such as `postgresql-npgsql` or
`postgresql-odbc`. The archives contain real provider implementations rather than per-driver binding DLLs.
For example, both PostgreSQL archives contain `DubUrl.Provider.PostgreSql.dll` and a separate,
connector-specific `DubUrl.Provider.PostgreSql.deps.json`. The Npgsql archive describes Npgsql dependencies;
the ODBC archive describes System.Data.Odbc and the shared ODBC implementation.

`Providers/Build/ProviderBinding.Build.csproj` is a non-packable dependency collector used only during
packaging. Its temporary `ConnectorDependencies.dll` is excluded from every archive. Packaging merges the
collector's dependency edges into the selected implementation's entry, removes the collector library/runtime
entry, and writes the implementation-named `.deps.json`. Native, RID-specific, and satellite assets retain
their published metadata. The original provider DLL and publish metadata are not modified.

There is no generated `ProviderModule`, entry-point type name, custom binding assembly metadata, or empty
binding DLL in the connector. The definition's `assembly` names a real implementation selected from its
dialect attributes. `implementationAssemblies` names the provider assemblies to inspect, and the driver
invariant selects the attributed mapping. Core discovers attributes in an isolated context through the shared
`AttributeProviderModule`. Repeated driver locators for one database/mapper are combined.
The `DubUrl` package (`DubUrl.Core.dll`) and its runtime dependency closure (including Didot) are supplied by the host and excluded from connector archives. Core generates a dependency inventory from its resolved NuGet assets; packaging and the connector loader use that same inventory. Driver and provider dependencies outside that closure remain private to each connector.

Core's default `ConnectionUrl(string)` constructor automatically discovers active, compatible,
verified connectors in the per-user store. `DUBURL_PROVIDER_STORE` overrides the store location.
Installed aliases take precedence over bundled `DubUrl` registrations; duplicate installed aliases
are errors. Explicit registries bypass discovery. New default registries observe store changes,
while existing URLs retain their selected providers.

Every connector dependency set is collected with `Providers/Build/ProviderBinding.Build.csproj`, using the settings in `connector-matrix.json`. There is no PostgreSQL/Npgsql-specific build path or project. Without overrides, the shared project builds PostgreSQL/Npgsql for local solution builds and focused consumer tests.

Database/driver combinations are distributed as connector packs, without per-driver binding DLLs. The collector is not packable, and the release workflow does not publish a PostgreSQL/Npgsql binding NuGet package.

`connector-matrix.json` is the maintained connector source. The official `connector-catalog.json` uses schema version 2 and contains `ConnectorDefinition` entries: connector ID/version, installation aliases, supported databases with their schemes, dependency assembly, implementation assemblies, driver selection, API range, target frameworks, and runtime identifiers. Each archive carries its own definition as `provider-pack.json`, using the same C# model and JSON structure. After creating an archive, the build enriches that exact definition with optional `downloadUri` and archive `sha256` fields for the catalog. Local definitions omit those distribution fields; Core reads them without a catalog or network access. Schema-v1 catalogs and older local definitions must be regenerated with the databases collection.

`Catalog/ConnectorCatalog.Build.csproj` combines the packaging matrix with the implementation attributes
to generate definitions. Ordinary `DubUrl.Application` builds embed the resulting `connector-catalog.json`; connector
packaging invokes the same exporter before adding distribution fields. The exporter targets the same
framework as its build, so a framework-scoped build needs no additional runtime. `BundledProviderCatalog.Create()`
reads the embedded definitions; `Describe()` groups schemes by database. `ConnectorService(store, httpClient)`
uses this catalog by default; hosts can supply a replacement catalog. The CLI uses the same default
unless a catalog file or catalog entries are supplied. It includes optional OLE DB schemes without
loading those drivers into the application.

The matrix uses source schema 2. It contains packaging choices: connector ID, a `databaseScheme`
selector referencing an existing database attribute alias (for example `pg`), the driver,
implementation family, installer aliases, runtimes, and prerequisites. It does not repeat mapper, dialect,
parametrizer, the complete URL scheme inventory, database display names, file-based markers, or factory type names. The technology table maps the driver
invariant to its build key and, only when different, its assembly name. The driver assembly supplies its
`DbProviderFactory` implementation through Core's shared factory-discovery helpers.

Discovery retains the attributed database type as its identity, along with its declared name and aliases.
Native and wrapper mappings retain the same underlying type; wrapper labels such as `ODBC for PostgreSQL`
are presentation only. Catalog names come directly from the database attribute, without parsing labels.
Installed registration validates the complete schemes and driver instead of using database names as selectors.
No database type names or additional ID inventory are serialized. Existing schema-2 catalogs and archives
remain compatible; only old source matrices need their display-name selector replaced with `databaseScheme`.

`DubUrl` is the provider-neutral NuGet package built by `DubUrl.Core/DubUrl.Core.csproj`. It contains
`DubUrl.Core.dll` with no embedded full catalog or registration wrapper. The all-provider aggregate and
the `DubUrl.Core` NuGet package are removed. Core finds provider assemblies named `DubUrl.Provider.*` in the
application's resolved runtime dependencies and the default load context. Installing `DubUrl`,
`DubUrl.Provider.PostgreSql`, and `Npgsql` is sufficient for `new ConnectionUrl("pg://localhost/demo")`;
no archive, catalog, or explicit factory registration is needed. Drivers resolve from application
runtime dependencies, while explicit `DbProviderFactories` registrations take precedence.
Installed archives retain priority over application providers for overlapping aliases.

Provider attributes are captured once per discovery pass, and resolved constructors and dialect instances
are retained in the registrations. URL resolution uses those registrations without rescanning provider
attributes. Installed providers retain their isolated driver factories.
ODBC locator options, shared dialect instances, and lazy attribute-declared BulkCopy adapters are preserved.

Definitions do not list assets. The installer verifies the complete archive SHA-256, safely extracts its files, and relies on the dependency assembly's `.deps.json` for dependency resolution. ZIP traversal, duplicate destination paths, and symbolic links are rejected before activation. Installed-file hashes are not tracked or rechecked; inspection still reports invalid definitions, incompatible packs, and missing required assemblies, while binary/dependency failures are reported during loading.

Provider-specific forward rewriting lives in the referenced `Providers/<Family>/DubUrl.Provider.*`
implementation projects, not in the connector composition modules. Rewriter source
and provider fixtures are colocated with their owning implementation package; see
[provider implementation packages](../Providers/README.md). Connector modules keep
responsibility for selecting the driver and registering the provider. Future reverse
conversion should follow the same implementation ownership boundary.

Provider implementations use their project-specific namespaces; see
[provider namespace migration](../Providers/NAMESPACES.md). Dialect types
use DubUrl.Provider.<Family>.Sql and mapper types use the owning provider's Mapping
namespace. Existing connector binaries must be rebuilt against the renamed public types.
Native DuckDB and SqlClient builds also include a typed bulk-copy adapter from
`DubUrl.Provider.DuckDb.Native.BulkCopy` or `DubUrl.Provider.SqlServer.SqlClient.BulkCopy`. These implementation
assemblies reference their driver packages; Core, the facade, and provider families do not.
Registration records the optional adapter name, and activation happens on bulk-copy execution.
ODBC/OLE DB builds do not include these adapters. The publish dependency closure carries the matching
adapter and its driver; hosts can deploy/load it without a compile-time driver reference.

For a local DuckDB artifact, `ProviderPackVerifier verify-bulk` accepts the same catalog,
archive, store, and provider arguments as `verify`. It checks a typed bulk-copy round trip,
including nulls, in the installed connector's load context using a Core-only host process.

The bundled Application catalog is an offline metadata snapshot. Archive download URLs and SHA-256
hashes are available only after archive packaging; use the published distribution catalog to install
those archives. Missing distribution information produces the existing explicit installation diagnostic.
Validate dependency-only and selective NuGet consumers with
`.github/scripts/Test-ProviderNuGetPackages.ps1 -NuGetDirectory <packages> -Version <version>`.

For single-file publishing, Core's NuGet build target keeps the existing application `.deps.json` beside
the executable. Core uses it to discover assemblies hidden inside the bundle. No provider-registration
list is generated. The Worker imports this target for its project-reference build as well.

## Shared archives and database installation aliases

The exporter groups selections by implementation assemblies, driver and runtime compatibility.
It reads the implementation attributes once, then includes every mapping for that implementation/driver.
Different drivers and implementation families remain separate; incidental shared dependencies do not
make different database implementations interchangeable.

| Canonical archive | Additional installation names |
| --- | --- |
| postgresql-npgsql | cockroachdb-npgsql, cratedb-npgsql, questdb-npgsql, timescale-npgsql |
| postgresql-odbc | questdb-odbc, timescale-odbc |
| mysql-mysqlconnector | mariadb-mysqlconnector |
| mysql-odbc | mariadb-odbc |

For example, `duburl connector install cockroachdb-npgsql` downloads `postgresql-npgsql` and activates
PostgreSQL, CockroachDB, CrateDB, QuestDB and Timescale mappings together. Installing a second alias
reuses that same canonical installation; uninstalling through any alias removes that canonical archive.
Existing short installation aliases are retained too. The other 20 archives remain unchanged in identity.

Catalog schema 2 and local definitions use `databases: [{name, schemes}, ...]`. There is no separate
`databaseName` or top-level scheme inventory; the C# Schemes property derives the union. The loader
requires the complete database/scheme inventory to match implementation attributes before registering
all mappings with one shared, isolated driver factory. The Application metadata view groups schemes
by database across these archive entries.
