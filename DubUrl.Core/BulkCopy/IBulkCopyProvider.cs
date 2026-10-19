namespace DubUrl.BulkCopy;

/// <summary>Creates native bulk-copy engines supplied by a provider or connector.</summary>
public interface IBulkCopyProvider
{
    IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies);
}
