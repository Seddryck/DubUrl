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
