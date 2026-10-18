---
title: Connection source reference
subtitle: Custom selection rules, provider support, and resource lifetime
---

Start with [Reusing database connections](connection-source) for examples. This page covers configuration and behavior in more detail.

## Add a custom selection rule

Implement `IConnectionReusePolicyResolver` when DubUrl's built-in Auto rules do not cover your provider or application requirements:

```csharp
sealed class MyReusePolicy : IConnectionReusePolicyResolver
{
    public ConnectionReuseMode? Resolve(ConnectionReuseContext context)
    {
        if (context.ProviderName == "MyProvider.MyFactory")
            return ConnectionReuseMode.RetainedExclusive;

        return null;
    }
}
```

Replace `MyProvider.MyFactory` with the full factory type name for your provider. Returning `null` means "this rule does not apply." Return `RetainedExclusive` only when keeping a connection open and allowing one operation at a time suits your application.

Pass the rule when creating the source:

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions
    {
        ReuseMode = ConnectionReuseMode.Auto,
        PolicyResolvers = [new MyReusePolicy()]
    });
```

Rules run once, when the source is created. DubUrl does not open a connection to choose the mode.

`Auto` is the default for `ConnectionSourceOptions` and for `new ConnectionSourceUrl(url).Create()`. The example sets it explicitly to show when these rules apply. Existing `DatabaseUrl` constructors that create their own source continue to use `ProviderManaged`; passing a source into `DatabaseUrl` uses that source's selected mode.

Selection follows this order:

1. If you explicitly choose `ProviderManaged` or `RetainedExclusive`, use that mode and skip all rules.
2. In `Auto` mode, try your `PolicyResolvers` in order. Use the first non-null result.
3. If none applies, use DubUrl's built-in provider rule.
4. If the provider or configuration is unknown, use `ProviderManaged`.

A rule must return a concrete mode or `null`; returning `Auto` is an error.

The context exposes `ProviderFactory` and `ProviderName`. Call `CreateConnectionStringBuilder()` if your rule needs to inspect the provider's effective connection settings. That builder may contain passwords: do not log it or its connection string. The context's own `ToString()` redacts the settings.

## Built-in provider rules

| Factory | Configuration used by the rule | Result |
| --- | --- | --- |
| `Npgsql.NpgsqlFactory` | Pooling setting, parsed by the provider's connection string builder | `ProviderManaged` |
| `Microsoft.Data.SqlClient.SqlClientFactory` | Pooling setting, including the provider's aliases and yes/no values | `ProviderManaged` |
| `Microsoft.Data.Sqlite.SqliteFactory` | Pooling setting, parsed by the provider's connection string builder | `ProviderManaged` |
| `DuckDB.NET.Data.DuckDBClientFactory` | Local `DataSource` / `Data Source`, including in-memory databases | `RetainedExclusive` |
| Other factories, including ODBC and OLE DB; DuckDB with an `md:` MotherDuck data source | No built-in rule | `ProviderManaged` |

The pooling rules distinguish enabled pooling from disabled pooling in `SelectionReason`. Both select `ProviderManaged`: disabling pooling alone does not establish that retention is appropriate. Unknown driver/proxy behavior is not guessed or tested by opening connections. Invalid recognized settings raise a validation error.

Local DuckDB retention keeps the database instance and connection caches available between operations. For an isolated in-memory database, this also keeps the data available across sequential leases while that connection remains healthy. This policy trades concurrent connections for one connection used by one operation at a time.

The rules were validated with Npgsql 10.x, Microsoft.Data.SqlClient 7.x, Microsoft.Data.Sqlite 10.x, and DuckDB.NET.Data 1.5.x. SQLite's Pooling option is available from version 6.x.

Provider background: [Npgsql pooling options](https://www.npgsql.org/doc/connection-string-parameters), [SqlClient pooling](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling), [SQLite connection settings](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings), [DuckDB.NET in-memory databases](https://github.com/Giorgi/DuckDB.NET-Docs/blob/main/docs/connection-string.md), and [DuckDB connection reuse](https://duckdb.org/docs/stable/guides/performance/how_to_tune_workloads).

## Cancellation and connection failures

Pass a cancellation token to `AcquireAsync(token)` to cancel waiting for a retained connection or opening a new connection. Canceling a waiting operation does not cancel the operation currently using the connection.

Canceling an acquisition also leaves an existing healthy retained connection available for later operations. It does not discard that connection or its in-memory data.

If opening a connection fails, DubUrl disposes resources created for that attempt. A later acquisition can try again. If a retained connection is found closed or broken, DubUrl discards it and opens a replacement. It does not test server reachability on every acquisition or rerun failed commands.

## Session settings and transactions

A retained connection can keep settings from one lease to the next. DubUrl does not reset arbitrary provider session state. Dispose each transaction before releasing its lease, and keep work requiring a particular transaction or session within one lease. Do not rely on separate leases to preserve that state: a failed connection may be replaced.

## Source and lease disposal

Finish using each lease before disposing its source. Calling source disposal while holding a lease in the same scope can make disposal wait for a lease that your code has not yet released.

When source disposal starts, it rejects new acquisitions, wakes queued waiters with `ObjectDisposedException`, and waits for outstanding leases to finish. It then disposes the retained connection and any provider data source it owns. Repeated disposal of a lease or source releases its resources only once.

Repeated disposal calls wait for the same cleanup and report the same failure, if cleanup fails. Reader disposal follows the same rule. `Dispose()` uses the provider's synchronous cleanup; `DisposeAsync()` uses its asynchronous cleanup when available. A throwing provider cancellation callback does not stop source cleanup: the disposal operation reports that failure after cleanup finishes. If several cleanup steps fail, it reports them together in an `AggregateException`.

Sources use the provider's `DbDataSource` when supported, with a connection-factory fallback for providers that do not implement data source creation. Existing `ConnectionUrl` and `DataSourceUrl` APIs keep their existing ownership behavior.

## Extending DatabaseUrl

`DatabaseUrl` and MicroOrm use the shared query executor for public query methods. Overriding the protected `PrepareCommand` helper does not intercept all those methods. Code using that helper must dispose its returned owning command or reader.

Subclasses that own additional resources can override `Dispose(bool)` for synchronous cleanup and `DisposeAsyncCore()` for asynchronous cleanup. Call the matching base implementation from each override.

`IDatabaseUrl` now extends `IDisposable` and `IAsyncDisposable`. If you implement that interface yourself, implement those lifecycle methods too.
