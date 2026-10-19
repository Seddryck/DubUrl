# Provider namespaces

The root namespace of each provider implementation is its exact project name:
`DubUrl.Provider.<Family>`. Source folders follow the namespace below that root.

| Folder and namespace suffix | Ownership |
| --- | --- |
| `Metadata` | Database identity, aliases, category, and branding |
| `Mapping` | Provider mappers |
| `Rewriting` | URL to connection-string translation |
| `Conversion` | Connection-string to URL translation |
| `Connectivity` | Technology-specific connectivity implementations |
| `Sql` | Query dialects |
| `Sql.Rendering` | SQL renderers |
| `Sql.Formatting` | Identifier and literal formatters, keyword resources |
| `Sql.Types` | SQL type-name mappings |
| `Sql.Functions` | SQL function mappings |
| `Sql.Casting` | Provider-specific result conversions |
| `Schema.Generation` | DDL capabilities and provider-specific schema SQL overrides |
| `Schema.Discovery` | Future catalog queries, metadata readers, and discovery capabilities |
| `Drivers.Discovery` | Installed-driver listing, matching, selection, and locator factories |
| `Tests.<Area>` | Provider-owned fixtures, following the production areas |

`Schema.Generation` and `Schema.Discovery` are separate responsibilities. The existing
generation capability declarations live in `Schema/Generation`; dialects select them
through the shared Core contract. Discovery is reserved for future implementation:
there are no placeholder discoverers, empty APIs, or new discovery support in this change.
Use `Schema` itself only for components genuinely shared by both operations.

ODBC discovery belongs to `DubUrl.Provider.Odbc.Drivers.Discovery`. It discovers
installed drivers, not database objects. Shared driver contracts and generic factory
mechanisms remain in Core. ODBC adapters retain their implementation ownership in the
ODBC provider; thin connector modules retain driver binding and registration.

Family projects retain their existing grouping: PostgreSQL-compatible databases share
`DubUrl.Provider.PostgreSql`, and MySQL-compatible databases share
`DubUrl.Provider.MySql`. Do not add another namespace level per database until needed.
There is no generic `Implementation` namespace. Class names and assembly/package IDs
remain unchanged by this reorganization.

## Migration

Driver bindings use `DubUrl.Provider.<Database>.<Driver>`, for example
`DubUrl.Provider.PostgreSql.Npgsql` and `DubUrl.Provider.CockroachDb.Npgsql`.
The latter reuses the PostgreSQL family through a dependency, without repeating that
family in its binding name. Family projects and typed adapters are grouped under
`Providers/<Family>/`. Optional adapters append `.BulkCopy`
to the driver's binding name. All standard bindings are built by
`Providers/Build/ProviderBinding.Build.csproj`; independent consumer tests are
`DubUrl.Provider.Testing`.

Binding assembly, package, and namespace names follow this convention. Consumers must
update package and namespace references and rebuild packs. Installable connector IDs
and URL schemes are unchanged; connector packs remain the distribution format.

Every production driver binding exposes `<binding namespace>.ProviderModule`, generated
from `Providers/Build/ProviderBinding.props`. Those entry points use the same
`DubUrl.Mapping.AssemblyMetadataProviderModule` implementation in Core. PostgreSQL/Npgsql
has no special registration class or handwritten registration logic.

Public fully qualified type names change. For example:

| Previous type | New type |
| --- | --- |
| `DubUrl.Mapping.Database.PostgresqlDatabase` | `DubUrl.Provider.PostgreSql.Metadata.PostgresqlDatabase` |
| `DubUrl.Mapping.Implementation.PostgresqlMapper` | `DubUrl.Provider.PostgreSql.Mapping.PostgresqlMapper` |
| `DubUrl.Querying.Dialects.PgsqlDialect` | `DubUrl.Provider.PostgreSql.Sql.PgsqlDialect` |
| `DubUrl.Locating.OdbcDriver.DriverLister` | `DubUrl.Provider.Odbc.Drivers.Discovery.DriverLister` |

Consumers must update imports and fully qualified references and recompile. Existing
connector binaries referencing the former names must be rebuilt together with these
providers; assembly forwarding cannot preserve a renamed namespace. No legacy namespace
wrappers are introduced. Review persisted assembly-qualified names and reflection-based
configuration as well as source references before upgrading. Core's shared contracts
retain their namespaces, and existing connection URL aliases remain unchanged.

Bulk-copy adapters belong to their optional driver binding projects and use those exact
project namespaces: `DubUrl.Provider.DuckDb.Native.BulkCopy` and
`DubUrl.Provider.SqlServer.SqlClient.BulkCopy`. Provider mapper declarations name the adapter
types without referencing their assemblies. Shared bulk-copy contracts retain the
`DubUrl.BulkCopy` namespace in Core; provider families remain driver independent.

Generated connectors and the documentation generator use the new names. DuckDB's keyword
resources use `DubUrl.Provider.DuckDb.Sql.Formatting.ReservedKeywords` and pass
that provider namespace explicitly to the shared keyword reader. Namespace/folder
boundaries, keyword loading, catalog resolution, and schema generation are tested.
