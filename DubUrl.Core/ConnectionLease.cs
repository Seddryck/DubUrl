using System.Data;
using System.Data.Common;

namespace DubUrl;

/// <summary>Exclusive borrowed use of a connection. Dispose commands and readers before releasing the lease.</summary>
public sealed class ConnectionLease : IDisposable, IAsyncDisposable
{
    private Func<bool, ValueTask>? _release;
    internal IDbConnection Handle { get; }

    /// <summary>The borrowed connection. Do not close or dispose it, or use it after releasing this lease.</summary>
    public DbConnection Connection => Handle as DbConnection
        ?? throw new NotSupportedException("This connection does not implement DbConnection.");

    internal ConnectionLease(IDbConnection connection, Func<bool, ValueTask> release)
        => (Handle, _release) = (connection, release);

    public void Dispose() => Release(false).GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => Release(true);

    private ValueTask Release(bool asynchronous)
        => Interlocked.Exchange(ref _release, null)?.Invoke(asynchronous) ?? default;
}
