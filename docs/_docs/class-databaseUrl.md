---
title: DatabaseUrl API
parent: Reference
nav_order: 5
permalink: /docs/class-databaseUrl/
---

DatabaseUrl executes SQL from a connection URL or a reusable connection source. Start with [Execute your first query]({{ '/docs/first-query/' | relative_url }}) for a walkthrough.

## Construction and ownership

| Constructor | Connection management | Source ownership |
|---|---|---|
| `DatabaseUrl(string url)` or `DatabaseUrl(factory, url)` | Per-operation `ProviderManaged` | DatabaseUrl owns its lazily created source |
| `DatabaseUrl(connectionUrl, provisionerFactory, logger)` | Per-operation `ProviderManaged` | DatabaseUrl owns its lazily created source |
| `DatabaseUrl(ConnectionSource source)` | Uses the source's selected mode | Caller owns the source |

Dispose DatabaseUrl when finished. A borrowed source remains caller-owned; dispose all readers and finish enumerating rows before disposing the source. See [Reuse connections and transactions]({{ '/docs/connection-source/' | relative_url }}) for examples and policy selection.
## Choose a query method

| What you need | Method | Empty result | More than one row |
| --- | --- | --- | --- |
| One value, such as a count | `ReadScalar<T>` | Read SQL NULL with a suitable nullable/reference type | Uses the first column of the first row |
| A value that must be present | `ReadScalarNonNull<T>` | Throws `InvalidOperationException` for a missing/null value | Uses the first column of the first row |
| The first row, if any | `ReadFirst` | Returns null | Uses the first row |
| The first row, which must exist | `ReadFirstNonNull` | Throws `InvalidOperationException` | Uses the first row |
| At most one row | `ReadSingle` | Returns null | Throws `InvalidOperationException` |
| Exactly one row | `ReadSingleNonNull` | Throws `InvalidOperationException` | Throws `InvalidOperationException` |
| All rows, read one at a time | `ReadMultiple` | Produces no rows | Produces each row |

The corresponding asynchronous methods have an `Async` suffix. Pass a cancellation token as the last argument when needed.

For nullable results and provider-specific CLR types, see [Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).

## Command timeout exceptions

`DatabaseUrl` throws `DubUrlCommandTimeoutException` when a provider reports a recognized command timeout. It derives from `TimeoutException` and exposes the original provider exception as `InnerException`. This applies to synchronous and asynchronous execution and to reading rows, columns, and subsequent result sets. Existing catches for provider exception types should catch `DubUrlCommandTimeoutException` for these failures.

DubUrl checks these diagnostics only while executing a command or reading its results. Connection acquisition, caller cancellation, and errors with ambiguous diagnostics propagate in their original form. Commands executed directly on a connection returned by `ConnectionUrl.Open()` use the provider's native behavior.

| Provider or transport | Diagnostic used by DatabaseUrl |
| --- | --- |
| Microsoft.Data.SqlClient and System.Data.SqlClient | `SqlException.Number == -2`. [SqlClient timeout errors](https://learn.microsoft.com/en-us/sql/connect/ado-net/configurable-retry-logic?view=sql-server-ver17) |
| Npgsql, including PostgreSQL-compatible databases | `NpgsqlException` with an immediate `TimeoutException` inner exception. PostgreSQL `57014` identifies query cancellation and is ambiguous on its own. [Npgsql command cancellation](https://github.com/npgsql/npgsql/blob/v10.0.3/src/Npgsql/Internal/NpgsqlConnector.cs#L1623-L1635) |
| MySqlConnector | `MySqlException.Number == -1` (`CommandTimeoutExpired`). `QueryInterrupted` also represents ordinary cancellation. [MySqlConnector cancellation behavior](https://mysqlconnector.net/overview/command-cancellation/) |
| SingleStoreConnector | `SingleStoreException.Number == -1` (`CommandTimeoutExpired` in the driver). [SingleStore driver](https://github.com/memsql/SingleStoreNETConnector) |
| ODBC | A diagnostic record has SQLSTATE `HYT00` (query timeout), with no `HYT01` (connection timeout) or `HY008` (cancellation) in the same error. [ODBC fetch diagnostics](https://learn.microsoft.com/en-us/sql/odbc/reference/syntax/sqlfetch-function?view=sql-server-ver17) |
| OLE DB | Same SQLSTATE rule as ODBC when the driver supplies diagnostic records. `DB_E_ABORTLIMITREACHED` alone can indicate other resource limits. [OLE DB execution diagnostics](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ms718095(v=vs.85)) |
| FirebirdSql.Data.FirebirdClient | A Firebird error record has number `335545127`, `335545128`, or `335545129` (statement timeout set by server configuration, attachment, or statement). [Firebird provider error codes](https://github.com/FirebirdSQL/NETProvider/blob/master/src/FirebirdSql.Data.FirebirdClient/Common/IscErrorMessages.cs) |
| Teradata.Client | A `TdError.Number` of `100038` in the exception's error collection. [Teradata provider error codes](https://teradata-docs.s3.amazonaws.com/doc/connectivity/tdnetdp/17.20/help/ErrorMessages.html) |
| Snowflake.Data | `OperationCanceledException` with an immediate `SnowflakeDbException` whose `ErrorCode` is `270007` (`REQUEST_TIMEOUT`). The driver's caller-cancellation detail is `270005`. [Snowflake statement execution](https://github.com/snowflakedb/snowflake-connector-net/blob/v6.3.0/Snowflake.Data/Core/SFStatement.cs) |
| ADBC, including DuckDB over ADBC | `AdbcException.Status == Timeout` (12). ADBC defines a separate status for cancellation. [ADBC status codes](https://arrow.apache.org/docs/16.0/format/ADBC/C.html) |
| Oracle.ManagedDataAccess | Native exception propagates. `ORA-01013` also represents user cancellation. [ODP.NET timeout behavior](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/CommandCommandTimeout.html) |
| IBM.Data.Db2 | Native exception propagates. `SQL0952N` covers timeout-driven and user-driven interrupts. [Db2 message reference](https://www.ibm.com/docs/en/db2/11.5.x?topic=SSEPGG_11.5.0%2Fcom.ibm.db2.luw.messages.sql.doc%2Fsql0750-sql0999.htm) |
| Microsoft.Data.Sqlite | Native exception propagates. `SQLITE_BUSY` and `SQLITE_LOCKED` identify contention and can also be raised directly while reading. [Microsoft.Data.Sqlite error handling](https://learn.microsoft.com/dotnet/standard/data/sqlite/database-errors) [SQLite reader source](https://github.com/dotnet/efcore/blob/v10.0.12/src/Microsoft.Data.Sqlite.Core/SqliteDataRecord.cs) |
| MySql.Data | Native exception propagates. In live MySQL 8.4 testing, command timeout produced error number `0` with nested I/O and socket errors. That shape alone does not identify the command deadline. |
| DuckDB.NET.Data, NReco.PrestoAdo (Trino), and ADOMD.NET | Native exception propagates. DubUrl has no verified command-specific signal for these routes. |
| Other and custom providers | Native exception propagates unless the provider uses one of the supported transports or exception types above. |

## Task guides

- <span id="read-a-value"></span>[Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).
- <span id="read-rows"></span><span id="read-with-an-adonet-reader"></span>[Read one or many rows]({{ '/docs/read-rows/' | relative_url }}).
- <span id="reuse-a-sql-template"></span>[Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}).
- <span id="when-several-commands-need-one-transaction"></span>[Reuse connections and transactions]({{ '/docs/connection-source/' | relative_url }}).
