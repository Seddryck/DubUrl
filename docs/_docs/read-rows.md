---
title: Read one or many rows
parent: Querying data
nav_order: 3

---

The examples assume `database` is a configured DatabaseUrl and the Customer table exists. See [your first query]({{ '/docs/first-query/' | relative_url }}) for setup.

## Choose the expected number of rows

| Method | No rows | More than one row |
|---|---|---|
| `ReadFirst` | Returns null | Returns the first row |
| `ReadFirstNonNull` | Throws InvalidOperationException | Returns the first row |
| `ReadSingle` | Returns null | Throws InvalidOperationException |
| `ReadSingleNonNull` | Throws InvalidOperationException | Throws InvalidOperationException |
| `ReadMultiple` | Produces no rows | Streams each row |

Async equivalents use the `Async` suffix and accept a cancellation token.

## Read and stream rows

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

## When several commands need one transaction

Separate `DatabaseUrl` calls do not share a transaction. Acquire one connection lease from your source and execute all commands in the transaction on that connection. The [connection source guide]({{ '/docs/connection-source/' | relative_url }}) shows how.
