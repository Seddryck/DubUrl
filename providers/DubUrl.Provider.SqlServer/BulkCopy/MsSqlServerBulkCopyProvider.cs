namespace DubUrl.BulkCopy.Engines;

internal sealed class MsSqlServerBulkCopyProvider : IBulkCopyProvider
{
    public static MsSqlServerBulkCopyProvider Instance { get; } = new();

    public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies)
        => new MsSqlServerBulkCopyEngine(connectionUrl, new MsSqlServerBulkCopyFactory(), dependencies);
}
