---
title: Connection sources
---

`ConnectionSourceUrl` creates a long-lived `ConnectionSource` on .NET 8, 9, and 10. A source lends open connections through short-lived leases. Existing `ConnectionUrl` and `DataSourceUrl` ownership semantics are unchanged.

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions { ReuseMode = ConnectionReuseMode.RetainedExclusive });

await using (var lease = await source.AcquireAsync(cancellationToken))
{
    await using var command = lease.Connection.CreateCommand();
    command.CommandText = "SELECT 1";
    var result = await command.ExecuteScalarAsync(cancellationToken);
}
// A later lease reuses the retained healthy open connection.
```

The default `ProviderManaged` mode opens a logical connection for each lease and disposes it when released. Physical pooling and reuse remain the provider's responsibility. `RetainedExclusive` keeps one healthy connection open between leases and serializes access. Source creation opens no connection. Sources use the provider's `DbDataSource` when available, with a factory-based fallback for providers that do not implement it. `Acquire()` and `Dispose()` are synchronous alternatives.

Dispose readers, commands, and transactions before their lease. Never directly close/dispose a borrowed connection or use it after lease release. Keep all work requiring one transaction or session within one lease. Retained connections may preserve session settings across leases; DubUrl cannot generically reset them. Separate leases do not guarantee transaction or session continuity.

Release leases before disposing their owning source: disposal rejects new acquisitions, wakes queued waiters with `ObjectDisposedException`, waits for active leases, and disposes owned connections and provider sources. Both source and lease disposal are idempotent. A second retained acquisition while holding a lease waits; nested acquisition is unsupported. Pass cancellation to bound waiting; cancellation of a waiter does not affect the active lease.

Failed opens dispose partial resources. A retained connection observed as closed or broken is discarded before the next acquisition. No generic connectivity probe or command replay is performed. All strategies share this contract; choose retention only for a workload that accepts serialized access and retained session state.

## Automatic strategy selection

Set `ReuseMode = ConnectionReuseMode.Auto` to select once at source creation without opening a connection. Explicit concrete modes always win. In Auto mode, the first registered `PolicyResolvers` entry returning a concrete mode wins, followed by the built-in policy, followed by `ProviderManaged` for unknown configurations. `null` means a resolver declines the configuration. A resolver returning `Auto` is invalid. The ordinary default remains `ProviderManaged`.

| Resolved native factory | Recognized configuration | Automatic decision |
| --- | --- | --- |
| Npgsql (validated with 10.x) | Provider builder's Pooling setting/default | ProviderManaged; enabled pooling reason or disabled-without-retention-policy reason |
| Microsoft.Data.SqlClient (validated with 7.x) | Provider builder's Pooling setting/default, including yes/no values | ProviderManaged with the corresponding reason |
| Microsoft.Data.Sqlite (validated with 10.x; Pooling available since 6.x) | Provider builder's Pooling setting/default | ProviderManaged with the corresponding reason |
| DuckDB.NET.Data (validated with 1.5.x) | Local file or in-memory DataSource/Data Source; excludes md: MotherDuck | RetainedExclusive |
| ODBC, OLE DB, MotherDuck, other factories | No recognized lifetime policy | ProviderManaged fallback |

Builders handle their provider's option aliases, defaults, and validation. Disabled pooling alone never enables retention. DuckDB local retention is an explicit DubUrl policy: it preserves the database instance for isolated in-memory connections and connection caches for repeated queries, at the cost of serialized access and retained state. See [DuckDB.NET connection strings](https://github.com/Giorgi/DuckDB.NET-Docs/blob/main/docs/connection-string.md) and [DuckDB connection performance guidance](https://duckdb.org/docs/stable/guides/performance/how_to_tune_workloads). Pooling defaults are documented by [Npgsql](https://www.npgsql.org/doc/connection-string-parameters), [SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/sql-server-connection-pooling), and [Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings).

Inspect `source.ReuseMode` and `source.SelectionReason` for the concrete decision and a credential-free policy identifier. Custom policies receive a `ConnectionReuseContext` with provider identity and `CreateConnectionStringBuilder()` for effective configuration. Never log that builder or its credential-bearing values. The context's default string representation redacts configuration. Unknown underlying drivers/proxies are not probed. For custom retention policies, explicitly establish that serialized reuse and retained session state are appropriate for your workload.

```csharp
sealed class MyReusePolicy : IConnectionReusePolicyResolver
{
    public ConnectionReuseMode? Resolve(ConnectionReuseContext context)
        => context.ProviderName == "MyProvider.MyFactory"
            ? ConnectionReuseMode.RetainedExclusive
            : null;
}

// Pass policy instances in source options (including from provider extension packages).
// new ConnectionSourceOptions { ReuseMode = ConnectionReuseMode.Auto,
//     PolicyResolvers = [new MyReusePolicy()] }
```

## DatabaseUrl integration

URL/factory constructors remain available. Dispose each `DatabaseUrl` instance: it lazily owns one provider-managed source and reuses that source across queries. Its sync/async scalar, first/single, streaming and raw-reader paths all acquire leases. MicroOrm shares this execution path.

```csharp
await using var database = new DatabaseUrl(url);
var count = await database.ReadScalarAsync<long>("SELECT COUNT(*) FROM Customer");
```

To choose a strategy or share a source across database instances, inject it. The source carries its resolved query metadata, so there is no separate URL that could mismatch its dialect/parameterization. DatabaseUrl borrows the injected source and never disposes it.

```csharp
await using var source = new ConnectionSourceUrl(url).Create(
    new ConnectionSourceOptions { ReuseMode = ConnectionReuseMode.Auto });
await using var database = new DatabaseUrl(source);
var first = database.ReadFirst("SELECT CustomerId FROM Customer");
var second = await database.ReadScalarAsync<long>("SELECT COUNT(*) FROM Customer");
```

Disposing the database prevents further query execution through that instance, including deferred enumeration, but other borrowers can still use the source. Disposal of a database-owned source waits for its outstanding operations. Close raw readers and dispose enumerators before disposing their database/source. `ReadMultiple` acquires only when enumeration begins; finishing, throwing, or disposing the enumerator releases its lease. Async streaming follows the same rule. Never-enumerated results acquire nothing.

An `ExecuteReader` result owns its reader, command, and lease. `Close`, `Dispose`, or `DisposeAsync` releases them once in that order. While a reader/enumerator holds a retained lease, another query on that source waits. Nested querying on a retained source while consuming its reader is unsupported. Different DatabaseUrl calls do not form a transaction; use one explicit source lease for transaction-dependent work.

For subclasses, `PrepareCommand` remains a protected compatibility helper returning an owning command. Dispose it or its returned reader. Query execution uses the shared executor, as before; overriding `PrepareCommand` does not intercept all public query methods. Resource lifetime customization belongs in source policy, not direct connection disposal. `IDatabaseUrl` now exposes `IDisposable` and `IAsyncDisposable`; external implementations must implement that lifecycle.
