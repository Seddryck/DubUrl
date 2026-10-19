namespace DubUrl.BulkCopy.Engines;

internal sealed class DuckDbBulkCopyProvider : IBulkCopyProvider
{
    public static DuckDbBulkCopyProvider Instance { get; } = new();

    public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies)
        => new DuckDbBulkCopyEngine(connectionUrl, new DuckDbAppenderFactory(dependencies), dependencies);
}
