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

## SQL execution errors

`DatabaseUrl` and its MicroOrm API classify command execution and result-reading failures when a supported driver supplies a specific structured diagnostic.

- `DubUrlSqlSyntaxException` indicates a SQL syntax error.
- `DubUrlTableNotFoundException` indicates a missing table or table-like source, such as an Access query or a text file used as a table.
- `DubUrlConstraintViolationException` indicates an integrity constraint violation.

Each is a `DubUrlException`. Its `InnerException` contains the original provider exception and diagnostics. Ambiguous and unrecognized codes propagate as the original exception. For example, SQL Server error `208`, Oracle error `942`, and Snowflake error `002003` can refer to inaccessible objects. SQLite and DuckDB.NET provide a distinct code for constraint violations, while their generic parsing and preparation errors propagate unchanged.

The same behavior applies to asynchronous reads, streamed rows, and DubUrl's owned reader. Commands created and executed directly through a connection or connection lease follow the provider's exception behavior.

## Task guides

- <span id="read-a-value"></span>[Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).
- <span id="read-rows"></span><span id="read-with-an-adonet-reader"></span>[Read one or many rows]({{ '/docs/read-rows/' | relative_url }}).
- <span id="reuse-a-sql-template"></span>[Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}).
- <span id="when-several-commands-need-one-transaction"></span>[Reuse connections and transactions]({{ '/docs/connection-source/' | relative_url }}).
