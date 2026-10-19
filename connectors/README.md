# Connector publication matrix

`connector-matrix.json` is the authoritative inventory and build definition for DubUrl connector packs. Every row is emitted into the official catalog only after its archive, dependency closure, manifest integrity, module loading, activation, and removal lifecycle have been validated.

All 32 existing database/technology combinations are available. Managed connectors carry their ADO.NET dependency closure. DuckDB and SQLite additionally carry the native assets published by their upstream binding packages, with explicit runtime compatibility metadata.

ODBC and OLE DB connectors carry the .NET bridge but do not redistribute database-vendor drivers or providers. Their archives include an external-prerequisite notice, and the matching machine-level driver must be installed separately. Access and Excel connectors are restricted to supported Windows runtime identifiers.

Driver binding assemblies use `DubUrl.Provider.<Database>.<Driver>` names, declared explicitly
by each matrix row's `assemblyName`. Shared family reuse stays in project dependencies:
`DubUrl.Provider.CockroachDb.Npgsql`, for example, uses `DubUrl.Provider.PostgreSql` internally.
Connector IDs, archive names, URL schemes, and catalog identities describe installable packs and stay unchanged.

`Providers/Build/ProviderBinding.props` generates the same `<binding namespace>.ProviderModule`
entry point and assembly metadata for every driver binding, including PostgreSQL/Npgsql.
All generated entry points use Core's `AssemblyMetadataProviderModule` registration logic;
there is no handwritten PostgreSQL/Npgsql registration path. Metadata type resolution uses
the binding's load context, keeping installed drivers out of the host's default context.
The shared build project selects the family, mapper, dialect, parametrizer, and driver from
its matrix settings, using the same generation and registration path for every binding.
`DubUrl.Core` and its runtime dependency closure (including Didot) are supplied by the host and excluded from connector archives. Core generates a dependency inventory from its resolved NuGet assets; packaging and the connector loader use that same inventory. Driver and provider dependencies outside that closure remain private to each connector.

Core's default `ConnectionUrl(string)` constructor automatically discovers active, compatible,
verified connectors in the per-user store. `DUBURL_PROVIDER_STORE` overrides the store location.
Installed aliases take precedence over bundled `DubUrl` registrations; duplicate installed aliases
are errors. Explicit registries bypass discovery. New default registries observe store changes,
while existing URLs retain their selected providers.

Every standard binding is built with `Providers/Build/ProviderBinding.Build.csproj`, using the settings in `connector-matrix.json`. There is no PostgreSQL/Npgsql-specific build path or project. Without overrides, the shared project builds PostgreSQL/Npgsql for local solution builds and focused consumer tests.

Driver bindings, including `DubUrl.Provider.PostgreSql.Npgsql`, are distributed as connector packs. The shared build project is not packable, and the release workflow does not publish a PostgreSQL/Npgsql binding NuGet package.

`connector-matrix.json` is the maintained connector source. The official `connector-catalog.json` uses schema version 1 and contains `ConnectorDefinition` entries: connector ID/version, installation aliases, supported schemes, module assembly/type, API range, target frameworks, and runtime identifiers. Each archive carries its own definition as `provider-pack.json`, using the same C# model and JSON structure. After creating an archive, the build enriches that exact definition with optional `downloadUri` and archive `sha256` fields for the catalog. Local definitions omit those distribution fields; Core reads them without a catalog or network access. Older catalogs without module information must be regenerated.

`ConnectorCatalog.targets` generates those definitions from the matrix using only the .NET SDK. Ordinary `DubUrl` builds embed the resulting `connector-catalog.json`; connector packaging invokes the same generator before enriching definitions with distribution fields. The optional `databaseName` field comes from the matrix's database label. `BuiltInProviderCatalog.Create()` reads the embedded catalog and groups schemes by that name, preserving its public API without a separate maintained provider list. This metadata includes all catalog schemes, including optional OLE DB connectors; listing them does not register or load their drivers. Bundled registrations remain an explicit composition choice.

Definitions do not list assets. The installer verifies the complete archive SHA-256, safely extracts its files, and relies on the module's `.deps.json` for dependency resolution. ZIP traversal, duplicate destination paths, and symbolic links are rejected before activation. Installed-file hashes are not tracked or rechecked; inspection still reports invalid definitions, incompatible packs, and missing module files, while binary/dependency failures are reported during loading.

Provider-specific forward rewriting lives in the referenced `Providers/<Family>/DubUrl.Provider.*`
implementation projects, not in the connector composition modules. Rewriter source
and provider fixtures are colocated with their owning implementation package; see
[provider implementation packages](../Providers/README.md). Connector modules keep
responsibility for selecting the driver and registering the provider. Future reverse
conversion should follow the same implementation ownership boundary.

Provider implementations use their project-specific namespaces; see
[provider namespace migration](../Providers/NAMESPACES.md). Generated dialect type names
use DubUrl.Provider.<Family>.Sql and mapper names use the owning provider's Mapping
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
