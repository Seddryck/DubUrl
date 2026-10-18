---
title: Querying with DatabaseUrl
subtitle: Run queries without managing connections for each call
tags: [connection, quick-start]
---

`DatabaseUrl` opens a connection when a query needs one and releases it when the query finishes. Use it to read values and rows without writing ADO.NET command and connection code for every query.

The examples assume your database provider is [installed and registered](installation-ado-net-provider), and `url` contains your connection URL. SQL examples using `Customer` assume that table already exists.

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

With `new DatabaseUrl(url)`, each operation opens and disposes its connection object. The provider's pool can reuse the underlying database connection.

To let DubUrl choose how to reuse connections, create a source with `Auto` and pass it to `DatabaseUrl`:

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions
    {
        ReuseMode = ConnectionReuseMode.Auto
    });

await using var database = new DatabaseUrl(source);

var first = await database.ReadScalarAsync("SELECT 1");
var second = await database.ReadScalarAsync("SELECT 2");
```

For local DuckDB, `Auto` keeps one connection open between these calls. For Npgsql, it lets the provider handle pooling. You can also choose `ProviderManaged` or `RetainedExclusive` explicitly. See [Reusing database connections](connection-source) for the modes and complete examples.

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

## Read rows

Row methods return objects whose properties match the result column names. Use `dynamic` to access them:

```csharp
dynamic customer = database.ReadFirstNonNull(
    "SELECT CustomerId, FullName FROM Customer ORDER BY CustomerId");

Console.WriteLine(customer.FullName);
```

To read every row:

```csharp
foreach (dynamic customer in database.ReadMultiple(
    "SELECT CustomerId, FullName FROM Customer"))
{
    Console.WriteLine(customer.FullName);
}
```

`ReadMultiple` starts the query when iteration begins, and keeps its connection until the loop finishes. A `break` also releases it. Async iteration works the same way:

```csharp
await foreach (dynamic customer in database.ReadMultipleAsync(
    "SELECT CustomerId, FullName FROM Customer"))
{
    Console.WriteLine(customer.FullName);
}
```

When using `RetainedExclusive`, finish this loop before running another query on the same source. To run more queries for the returned rows, read them into a list first:

```csharp
var customers = database.ReadMultiple(
    "SELECT CustomerId, FullName FROM Customer").ToList();
// The connection has been released. You can now run another query.
```

## Read with an ADO.NET reader

Use `ExecuteReader` when you want to work with column values yourself:

```csharp
using (var reader = database.ExecuteReader(
    "SELECT CustomerId, FullName FROM Customer"))
{
    while (reader.Read())
        Console.WriteLine(reader.GetString(1));
} // Dispose the reader, its command, and its connection lease.
```

The reader holds the connection until you close or dispose it. Put it in a `using` block so it is released before the next operation. `ExecuteReaderAsync` returns a reader you can use with `await using` and `ReadAsync`.

## Reuse a SQL template

Create a template once and supply different values for each query. DubUrl uses `$...$` template expressions:

```csharp
var template = database.CreateTemplate("SELECT $value; format=\"value\"$");

var first = database.ReadScalar(template,
    new Dictionary<string, object?> { ["value"] = 1 });

var second = database.ReadScalar(template,
    new Dictionary<string, object?> { ["value"] = 2 });
```

The `value` format renders a SQL literal using the database dialect. This produces SQL text; it is not a bound ADO.NET parameter. Do not insert untrusted text directly into a query. The string, template, and `ICommandProvider` overloads all use the same connection handling described above.

## When several commands need one transaction

Separate `DatabaseUrl` calls do not share a transaction. Acquire one connection lease from your source and execute all commands in the transaction on that connection. The [connection source guide](connection-source) shows how.
