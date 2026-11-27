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

    /// <summary>Controls when the copy commits. Defaults to provider behavior.</summary>
    public BulkCopyTransactionMode TransactionMode { get; init; }

    /// <summary>The default number of rows per transaction in <see cref="BulkCopyTransactionMode.PerBatch"/> mode.</summary>
    public const int DefaultBatchSize = 1000;

    /// <summary>
    /// The positive number of rows per transaction in <see cref="BulkCopyTransactionMode.PerBatch"/> mode.
    /// When omitted, <see cref="DefaultBatchSize"/> is used. The final batch may contain fewer rows.
    /// </summary>
    public int? BatchSize { get; init; }

    /// <summary>Validates the options before a copy begins.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(TransactionMode))
            throw new ArgumentOutOfRangeException(nameof(TransactionMode), TransactionMode, "Unknown bulk-copy transaction mode.");
        if (BatchSize is <= 0)
            throw new ArgumentOutOfRangeException(nameof(BatchSize), BatchSize, "BatchSize must be positive.");
        if (BatchSize.HasValue && TransactionMode != BulkCopyTransactionMode.PerBatch)
            throw new ArgumentException("BatchSize requires the PerBatch transaction mode.", nameof(BatchSize));
    }
}
