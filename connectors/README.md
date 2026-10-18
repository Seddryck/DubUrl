# Connector publication matrix

`connector-matrix.json` is the authoritative inventory and build definition for DubUrl connector packs. Every row is emitted into the official catalog only after its archive, dependency closure, manifest integrity, module loading, activation, and removal lifecycle have been validated.

All 32 existing database/technology combinations are available. Managed connectors carry their ADO.NET dependency closure. DuckDB and SQLite additionally carry the native assets published by their upstream binding packages, with explicit runtime compatibility metadata.

ODBC and OLE DB connectors carry the .NET bridge but do not redistribute database-vendor drivers or providers. Their archives include an external-prerequisite notice, and the matching machine-level driver must be installed separately. Access and Excel connectors are restricted to supported Windows runtime identifiers.

The connector assemblies are generated from the matrix through a single explicit `IDubUrlProviderModule` implementation. Each build selects exactly one database family, mapper, dialect, parametrizer, and driver technology. `DubUrl.Core` is supplied by the host and is deliberately excluded from connector archives.

`DubUrl.Connector.PostgreSql.Npgsql` is also published as a NuGet package for applications that choose PostgreSQL/Npgsql at build time. It depends only on `DubUrl.Core`, `DubUrl.Provider.PostgreSql`, and Npgsql, with DubUrl dependency bounds from the current build version through, but excluding, `1.0.0`.

The official `connector-catalog.json` uses schema version 1. Every entry contains its canonical connector ID, optional command aliases, supported schemes, immutable HTTPS URL, archive SHA-256, DubUrl API range, target frameworks, and runtime identifiers.
