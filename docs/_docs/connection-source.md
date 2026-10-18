---
title: Reusing database connections
subtitle: Let DubUrl decide when to keep a connection open
tags: [connection, quick-start]
---

A `ConnectionSource` lets you run separate operations without deciding, each time, whether to close the connection or keep it open for the next operation.

Create the source once and reuse it. DubUrl can then either let the provider's connection pool handle reuse, or keep one connection open itself.

These features are available on .NET 8, 9, and 10. The examples assume you have [installed and registered your database provider](installation-ado-net-provider) and stored your connection URL in `url`.

## Run queries with DatabaseUrl

If you use `DatabaseUrl`, it handles acquiring and releasing connections for you:

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

Both queries use the same source. In `Auto` mode, DubUrl chooses how that source manages connections. For example, it lets Npgsql handle PostgreSQL pooling, but keeps one connection open for a local DuckDB database.

The `await using` declarations clean up in reverse order: first `database`, then `source`, at the end of the surrounding scope. Keep the source around for the operations that should benefit from reuse. Creating a new source for every query defeats that purpose.

## Choose what happens between operations

Set `ReuseMode` when creating the source:

| Mode | After an operation finishes | When the next operation starts |
| --- | --- | --- |
| `ProviderManaged` | Dispose the connection object. If the provider has pooling enabled, it can keep the underlying database connection in its pool. | Ask the provider for an open connection. It may reuse one from its pool. |
| `RetainedExclusive` | Keep the connection open in the source. | Reuse that same connection, provided it is still open and healthy. Only one operation can use it at a time. |
| `Auto` | Use the behavior selected by DubUrl's provider rules. | Follow the selected mode. |

If you omit the options, the default is `ProviderManaged`. To explicitly keep a connection open between operations, use:

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions
    {
        ReuseMode = ConnectionReuseMode.RetainedExclusive
    });
```

With this mode, the source closes the retained connection when you dispose the source.

## Use an ADO.NET connection directly

For direct ADO.NET access, call `AcquireAsync()`. It returns a **lease**: permission to use a connection until you dispose the lease.

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions
    {
        ReuseMode = ConnectionReuseMode.RetainedExclusive
    });

await using (var lease = await source.AcquireAsync())
{
    await using var command = lease.Connection.CreateCommand();
    command.CommandText = "SELECT 1";
    var first = await command.ExecuteScalarAsync();
} // Release the connection for the next operation. Keep it open.

await using (var lease = await source.AcquireAsync())
{
    await using var command = lease.Connection.CreateCommand();
    command.CommandText = "SELECT 2";
    var second = await command.ExecuteScalarAsync();
} // Release the same connection again.
```

The braces matter: the first lease must end before the second starts. Disposing the lease tells the source that the operation has finished. The source decides whether to dispose the connection or retain it.

Dispose your commands, readers, and transactions inside the lease's braces. Do not dispose `lease.Connection` yourself or use it after the lease ends. For synchronous code, use `source.Acquire()` and ordinary `using` blocks.

## What Auto currently chooses

| Provider | Selected mode |
| --- | --- |
| Npgsql / PostgreSQL | `ProviderManaged` |
| Microsoft.Data.SqlClient / SQL Server | `ProviderManaged` |
| Microsoft.Data.Sqlite / SQLite | `ProviderManaged` |
| DuckDB.NET.Data / local DuckDB, including in-memory databases | `RetainedExclusive` |
| ODBC, OLE DB, MotherDuck, and other unrecognized providers | `ProviderManaged` |

Disabling a provider's pooling does not automatically make DubUrl retain its connections. Choose `RetainedExclusive` explicitly if your workload needs that behavior, or register a custom rule.

To inspect the decision:

```csharp
Console.WriteLine(source.ReuseMode);
Console.WriteLine(source.SelectionReason);
```

See the [connection source reference](connection-source-reference) for custom rules and provider details.

## When operations need the same transaction

Keep all commands in that transaction inside **one lease**. Separate `DatabaseUrl` calls do not form a transaction, even if the source happens to reuse the same connection.

```csharp
await using var lease = await source.AcquireAsync();
await using var transaction = await lease.Connection.BeginTransactionAsync();

await using var command = lease.Connection.CreateCommand();
command.Transaction = transaction;

command.CommandText = "SELECT 1";
await command.ExecuteScalarAsync();

command.CommandText = "SELECT 2";
await command.ExecuteScalarAsync();

await transaction.CommitAsync();
```

## When another operation is still reading

`RetainedExclusive` lends its one connection to one operation at a time. If another operation asks for it, that operation waits.

Finish reading or dispose the reader before starting another query on that source. In particular, do not start a query inside a loop that is still reading from the same retained connection. Read the rows into a list first if you need to run more queries for those rows.

For details about cancellation, failures, and shutdown, see the [connection source reference](connection-source-reference).
