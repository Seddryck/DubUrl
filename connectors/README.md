# Connector publication matrix

`connector-matrix.json` is the source inventory for DubUrl connector packs. A row is `planned` until its redistribution rights, dependencies, native assets, target frameworks, runtime identifiers, and external prerequisites have been verified. Only `ready` rows may be emitted into the official catalog, and only after the release workflow has built and validated their immutable archives.

The first ready connector is `postgresql-npgsql`. Its pack contains an explicit provider module and Npgsql's managed dependency closure. It is portable across the CLI's `net8.0`, `net9.0`, and `net10.0` hosts and has no machine-level database-driver prerequisite.

The same module is also published as the `DubUrl.Connector.PostgreSql.Npgsql` NuGet package for applications that choose their connectors at build time. Its DubUrl dependency starts at the package's build version and stops before `1.0.0`; the downloadable connector pack and NuGet package are built from the same project output.

ODBC and OLE DB rows remain separate because their machine-installed drivers or providers are not pack-owned assets. Before those rows become ready, activation must probe the external prerequisite and return an actionable diagnostic when it is absent.

The official `connector-catalog.json` uses schema version 1. Every entry contains the canonical connector ID, pack version, aliases, schemes, immutable HTTPS URL, archive SHA-256, DubUrl API range, target frameworks, and runtime identifiers. It is generated from validated release artifacts; known mappings are never copied into it merely because DubUrl recognizes their URL schemes.
