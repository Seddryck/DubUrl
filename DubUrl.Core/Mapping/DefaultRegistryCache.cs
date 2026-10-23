using DubUrl.Core.ProviderPacks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Mapping;

/// <summary>Retains one default snapshot until the selected store or its revision changes.</summary>
internal sealed class DefaultRegistryCache(Func<ProviderPackStore, ProviderCatalogBuilder> createCatalog)
{
    private readonly object gate = new();
    private string? root;
    private string? revision;
    private Lazy<Snapshot>? snapshot;

    internal SchemeRegistry GetSchemes(ProviderPackStore store) => Get(store, value => value.Schemes);
    internal IDialectRegistry GetDialects(ProviderPackStore store) => Get(store, value => value.Dialects);

    internal void Invalidate()
    {
        lock (gate) snapshot = null;
    }

    private T Get<T>(ProviderPackStore store, Func<Snapshot, Lazy<T>> select)
    {
        lock (gate)
        {
            var currentRevision = store.ReadRevision();
            if (snapshot is { IsValueCreated: true } && root == store.RootPath && revision == currentRevision
                && select(snapshot.Value).IsValueCreated)
                return select(snapshot.Value).Value;

            using var storeLock = store.AcquireLock();
            currentRevision = store.ReadRevision();
            if (snapshot is null || root != store.RootPath || revision != currentRevision)
            {
                root = store.RootPath;
                revision = currentRevision;
                snapshot = new Lazy<Snapshot>(() => new(createCatalog(store)), LazyThreadSafetyMode.ExecutionAndPublication);
            }
            // Discovery and driver resolution must finish before releasing the store lock.
            try { return select(snapshot.Value).Value; }
            catch
            {
                // A missing dependency or failed load can be repaired; do not cache failures.
                snapshot = null;
                throw;
            }
        }
    }

    private sealed class Snapshot(ProviderCatalogBuilder catalog)
    {
        internal Lazy<SchemeRegistry> Schemes { get; } = new(catalog.Build, LazyThreadSafetyMode.ExecutionAndPublication);
        internal Lazy<IDialectRegistry> Dialects { get; } = new(catalog.BuildDialects, LazyThreadSafetyMode.ExecutionAndPublication);
    }
}
