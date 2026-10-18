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

## Task guides

- <span id="read-a-value"></span>[Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).
- <span id="read-rows"></span><span id="read-with-an-adonet-reader"></span>[Read one or many rows]({{ '/docs/read-rows/' | relative_url }}).
- <span id="reuse-a-sql-template"></span>[Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}).
- <span id="when-several-commands-need-one-transaction"></span>[Reuse connections and transactions]({{ '/docs/connection-source/' | relative_url }}).
