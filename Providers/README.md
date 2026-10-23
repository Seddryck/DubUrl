# Provider implementation packages

Projects are grouped by family under `Providers/<Family>/`. For example:

```text
Providers/DuckDb/
├── DubUrl.Provider.DuckDb/
└── DubUrl.Provider.DuckDb.QA/
```

Each QA project uses the singular `DubUrl.Provider.<Database>.QA` name and namespace.
PostgreSQL-compatible QA projects share the `PostgreSql` folder; SingleStore QA shares
`MySql`. Analysis Services and Power BI QA projects are grouped under `Adomd`.
Shared build settings live in `Providers/Provider.Build.props`.

DubUrl keeps provider-neutral contracts and runtime behavior in the `DubUrl` package (`DubUrl.Core.dll`). Database-specific dialects, mappers, rewriters, type mappings, and discovery code live in the following packages:

| Package | Database families |
| --- | --- |
| `DubUrl.Provider.Access` | Microsoft Access |
| `DubUrl.Provider.Db2` | IBM Db2 |
| `DubUrl.Provider.Drill` | Apache Drill |
| `DubUrl.Provider.DuckDb` | DuckDB |
| `DubUrl.Provider.Excel` | Microsoft Excel |
| `DubUrl.Provider.FirebirdSql` | Firebird SQL |
| `DubUrl.Provider.MySql` | MySQL, MariaDB, SingleStore |
| `DubUrl.Provider.Odbc` | Shared ODBC mapping, discovery, options, and ODBC adapters for the supported families |
| `DubUrl.Provider.Oracle` | Oracle Database |
| `DubUrl.Provider.PostgreSql` | PostgreSQL, CockroachDB, CrateDB, QuestDB, Timescale |
| `DubUrl.Provider.Snowflake` | Snowflake |
| `DubUrl.Provider.Sqlite` | SQLite |
| `DubUrl.Provider.SqlServer` | Microsoft SQL Server |
| `DubUrl.Provider.Teradata` | Teradata |
| `DubUrl.Provider.Text` | Delimited text files through ODBC |
| `DubUrl.Provider.Trino` | Trino |

Each shared family package above depends on `DubUrl` without a concrete ADO.NET driver.
Install a family package alongside its concrete ADO.NET driver, such as
`DubUrl.Provider.PostgreSql` and `Npgsql`. Core discovers their attributes and driver factory automatically.
Alternatively, provider archives carry the family implementation and selected driver together, with
an implementation-anchored `.deps.json` and local `provider-pack.json` definition. There are no empty
binding DLLs or per-driver binding NuGet packages.
The all-provider aggregate and the `DubUrl.Core` NuGet package are removed. The physical Core project,
`DubUrl.Core.dll`, and existing namespaces keep their names; its NuGet package is `DubUrl`.

Provider and connector project-reference dependencies are packed with a lower bound equal to their release version and an exclusive `1.0.0` upper bound. This keeps the modular packages on the compatible 0.x API line.

Provider-specific rewriting source files are physically owned by each package under
`Rewriting`, including their nested token mappers. PostgreSQL and
MySQL derivatives stay with their database family; ODBC adapters stay in
`DubUrl.Provider.Odbc`. The shared rewriter, token-mapping contracts, specificators,
and URL model remain in `DubUrl.Core`. Assembly identities remain unchanged; provider
namespaces now start with their owning project name.

Provider rewriting fixtures live under each package's `Tests/Rewriting`
directory. Production projects exclude `Tests/**/*.cs`; `DubUrl.Core.Testing` includes
these fixtures explicitly so the shared test runner and provider registrations are
preserved. Shared rewriting and cross-provider boundary tests remain central.

Database declarations, branding attributes, native mappers, and ODBC mappers are physically
owned under each provider's Metadata and Mapping directories. Provider-specific mapping fixtures live
under Tests/Mapping and are included by the shared DubUrl.Core.Testing runner. Core retains
only reusable mapping contracts and catalog infrastructure; connector modules select
the concrete driver and register these implementations.

Concrete dialects, SQL renderers, type/function mappings, database-specific formatters
and casters, and driver discovery sources also live in their owning provider projects.
DuckDB reserved-keyword resources live under Sql/Formatting and use the provider's
namespace in the DuckDb assembly. Shared ANSI components and locator contracts stay
in Core. Provider fixtures under Tests/Sql and Tests/Drivers/Discovery continue to
use the shared test runner.

See [namespace conventions and migration](NAMESPACES.md) for the complete layout,
including Schema.Generation, the reserved Schema.Discovery area, and ODBC's
Drivers.Discovery. Public provider namespaces change and consumers must recompile.

The isolated consumer `DubUrl.Provider.Testing` exercises PostgreSQL registration
with explicit family references and without friend access. The tests enforce source ownership, execute a
bound parameter through a fake driver, and reuse PostgreSQL behavior with a different
connector registration. See the provider modules documentation for setup and the
distinction between independent registration and official publication inventories.

DuckDB ADBC URL mappings belong to the DuckDB family package and use the optional
`DubUrl.Adbc` ADO.NET adapter. Register the adapter under
`DubUrl.Provider.DuckDb.Adbc` before building a registry, or install the
`DubUrl.Provider.DuckDb.Adbc` package / `duckdb-adbc` connector for automatic
factory and installed-driver discovery. Native libraries remain caller-installed. Core contains only
the shared ADBC locator contract and compound-scheme normalization.
