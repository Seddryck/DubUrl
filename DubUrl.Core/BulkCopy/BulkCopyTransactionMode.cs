namespace DubUrl.BulkCopy;

/// <summary>Controls when copied rows are committed.</summary>
public enum BulkCopyTransactionMode
{
    /// <summary>Use the selected provider's normal bulk-copy behavior.</summary>
    ProviderDefault = 0,

    /// <summary>Commit the entire copy in one transaction.</summary>
    WholeCopy = 1,

    /// <summary>Commit each batch in its own transaction. Earlier commits survive a later failure.</summary>
    PerBatch = 2
}
