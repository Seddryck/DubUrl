# Connector publication matrix

`connector-matrix.json` is the authoritative inventory and build definition for DubUrl connector packs. Every row is emitted into the official catalog only after its archive, dependency closure, manifest integrity, module loading, activation, and removal lifecycle have been validated.

All 32 existing database/technology combinations are available. Managed connectors carry their ADO.NET dependency closure. DuckDB and SQLite additionally carry the native assets published by their upstream binding packages, with explicit runtime compatibility metadata.

ODBC and OLE DB connectors carry the .NET bridge but do not redistribute database-vendor drivers or providers. Their archives include an external-prerequisite notice, and the matching machine-level driver must be installed separately. Access and Excel connectors are restricted to supported Windows runtime identifiers.

The connector assemblies are generated from the matrix through a single explicit `IDubUrlProviderModule` implementation. Each build selects exactly one database family, mapper, dialect, parametrizer, and driver technology. `DubUrl.Core` is supplied by the host and is deliberately excluded from connector archives.

`DubUrl.Connector.PostgreSql.Npgsql` is also published as a NuGet package for applications that choose PostgreSQL/Npgsql at build time. It depends only on `DubUrl.Core`, `DubUrl.Provider.PostgreSql`, and Npgsql, with DubUrl dependency bounds from the current build version through, but excluding, `1.0.0`.

The official `connector-catalog.json` uses schema version 1. Every entry contains its canonical connector ID, optional command aliases, supported schemes, immutable HTTPS URL, archive SHA-256, DubUrl API range, target frameworks, and runtime identifiers.

Provider-specific forward rewriting lives in the referenced `providers/DubUrl.Provider.*`
implementation projects, not in the connector composition modules. Rewriter source
and provider fixtures are colocated with their owning implementation package; see
[provider implementation packages](../providers/README.md). Connector modules keep
responsibility for selecting the driver and registering the provider. Future reverse
conversion should follow the same implementation ownership boundary.

Provider implementations use their project-specific namespaces; see
[provider namespace migration](../providers/NAMESPACES.md). Generated dialect type names
use DubUrl.Provider.<Family>.Sql and mapper names use the owning provider's Mapping
namespace. Existing connector binaries must be rebuilt against the renamed public types.
Native DuckDB and SqlClient builds also include a typed bulk-copy adapter from
`DubUrl.Connector.DuckDb.BulkCopy` or `DubUrl.Connector.SqlServer.BulkCopy`. These implementation
assemblies reference their driver packages; Core, the facade, and provider families do not.
Registration records the optional adapter name, and activation happens on bulk-copy execution.
ODBC/OLE DB builds do not include these adapters. The publish dependency closure carries the matching
adapter and its driver; hosts can deploy/load it without a compile-time driver reference.

For a local DuckDB artifact, `ConnectorArtifactVerifier verify-bulk` accepts the same catalog,
archive, store, and provider arguments as `verify`. It checks a typed bulk-copy round trip,
including nulls, in the installed connector's load context using a Core-only host process.
