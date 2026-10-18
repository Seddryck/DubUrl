---
title: Execute your first query
parent: Querying data
nav_order: 1

---

DatabaseUrl opens a connection when a query needs one and releases it when that operation finishes. Before using these examples, [install and register your provider]({{ '/docs/installation-ado-net-provider/' | relative_url }}), and set `url` to your connection URL.

## Read a value

```csharp
await using var database = new DatabaseUrl(url);

var count = await database.ReadScalarAsync(
    "SELECT COUNT(*) FROM Customer");

Console.WriteLine(count);
```

Keep the database object for the queries you want to run, and dispose it when finished. `await using` handles disposal at the end of the surrounding scope. For synchronous code:

```csharp
using var database = new DatabaseUrl(url);
var count = database.ReadScalar("SELECT COUNT(*) FROM Customer");
```

You can also use typed overloads, such as `ReadScalar<long>`. Choose a type that matches your provider's SQL result: some providers return `COUNT(*)` as a 32-bit integer and others as a 64-bit integer.

## Choose how connections are reused

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

## Next steps

- [Read scalar values]({{ '/docs/read-scalars/' | relative_url }}) for NULL handling.
- [Read one or many rows]({{ '/docs/read-rows/' | relative_url }}) for result cardinality and streaming.
- [DatabaseUrl API]({{ '/docs/class-databaseUrl/' | relative_url }}) for the method selection table.
