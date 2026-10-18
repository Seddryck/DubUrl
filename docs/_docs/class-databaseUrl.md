---
title: DatabaseUrl API
parent: Reference
nav_order: 5
permalink: /docs/class-databaseUrl/
---

DatabaseUrl executes SQL from a connection URL or a reusable connection source. Start with [Execute your first query]({{ '/docs/first-query/' | relative_url }}) for a walkthrough.

## Construction and ownership

With `new DatabaseUrl(url)`, each operation opens and disposes its connection object. The provider's pool can reuse the underlying database connection. Existing constructors that take a URL, factory, or `ConnectionUrl` keep this `ProviderManaged` behavior.

To let DubUrl choose how to reuse connections, create a source and pass it to `DatabaseUrl`. Sources use `Auto` by default:

```csharp
await using var source = new ConnectionSourceUrl(url).Create();

await using var database = new DatabaseUrl(source);

var first = await database.ReadScalarAsync("SELECT 1");
var second = await database.ReadScalarAsync("SELECT 2");
```

For local DuckDB, `Auto` keeps one connection open between these calls. For Npgsql, it lets the provider handle pooling. You can also choose `ProviderManaged` or `RetainedExclusive` explicitly. See [Reusing database connections]({{ '/docs/connection-source/' | relative_url }}) for the modes and complete examples.

You own a source you pass into `DatabaseUrl`. Disposing the database object leaves that source available for other database objects. In the example, the declarations dispose `database` first, then `source`.

If you use a custom scheme registry, you can still construct a database with `new DatabaseUrl(factory, url)`, where `factory` is your configured `ConnectionUrlFactory`.

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

For SQL values that may be NULL, choose a nullable result type:

```csharp
var oldest = await database.ReadScalarAsync<DateTime?>(
    "SELECT MIN(BirthDate) FROM Customer");
```

## Task guides

- [Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).
- [Read one or many rows]({{ '/docs/read-rows/' | relative_url }}).
- [Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}).
- [Reuse connections and transactions]({{ '/docs/connection-source/' | relative_url }}).

## Read a value

See [Read scalar values]({{ '/docs/read-scalars/' | relative_url }}).

## Read rows

See [Read one or many rows]({{ '/docs/read-rows/' | relative_url }}).

## Read with an ADO.NET reader

See [Read with an ADO.NET reader]({{ '/docs/read-rows/#read-with-an-adonet-reader' | relative_url }}).

## Reuse a SQL template

See [Use SQL templates]({{ '/docs/sql-templates/' | relative_url }}).

## When several commands need one transaction

See [the connection source guide]({{ '/docs/connection-source/' | relative_url }}).
