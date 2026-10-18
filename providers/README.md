# Provider implementation packages

DubUrl keeps provider-neutral contracts and runtime behavior in `DubUrl.Core`. Database-specific dialects, mappers, rewriters, type mappings, and discovery code live in the following packages:

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

Each package depends on `DubUrl.Core` but not on a concrete ADO.NET driver. Connector packages combine one or more provider implementation packages with a particular driver and an explicit `IDubUrlProviderModule`. The `DubUrl` package remains the 0.x compatibility aggregate and references every provider implementation package.

Provider and connector project-reference dependencies are packed with a lower bound equal to their release version and an exclusive `1.0.0` upper bound. This keeps the modular packages on the compatible 0.x API line.
