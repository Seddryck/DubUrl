namespace DubUrl.BulkCopy;

/// <summary>Options for copying rows into a database table.</summary>
public readonly record struct BulkCopyOptions
{
    /// <summary>
    /// Requests a table-level lock on the destination during the copy.
    /// The selected provider determines which concurrent operations the lock blocks.
    /// Providers that cannot honor this request throw <see cref="NotSupportedException"/>.
    /// </summary>
    public bool TableLock { get; init; }
}
