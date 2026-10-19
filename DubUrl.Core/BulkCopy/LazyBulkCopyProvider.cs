using System.Data;

namespace DubUrl.BulkCopy;

/// <summary>Activates a typed adapter from an optional connector assembly when bulk copy is executed.</summary>
public sealed class LazyBulkCopyProvider : IBulkCopyProvider
{
    public string AssemblyName { get; }
    public string TypeName { get; }

    public LazyBulkCopyProvider(string assemblyName, string typeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        AssemblyName = assemblyName;
        TypeName = typeName;
    }

    public IBulkCopyEngine Create(ConnectionUrl connectionUrl, IBulkCopyDependencyResolver dependencies)
    {
        ArgumentNullException.ThrowIfNull(connectionUrl);
        ArgumentNullException.ThrowIfNull(dependencies);
        return new DeferredEngine(this, connectionUrl, dependencies);
    }

    private sealed class DeferredEngine(LazyBulkCopyProvider provider, ConnectionUrl connectionUrl,
        IBulkCopyDependencyResolver dependencies) : IBulkCopyEngine
    {
        public void Write(string tableName, IDataReader dataReader)
        {
            var driverAssembly = connectionUrl.GetProviderFactory().GetType().Assembly;
            dependencies.Execute(driverAssembly, () =>
            {
                var type = dependencies.ResolveType(driverAssembly, provider.AssemblyName, provider.TypeName);
                if (!typeof(IBulkCopyProvider).IsAssignableFrom(type))
                    throw new InvalidOperationException($"Bulk-copy adapter '{provider.TypeName}' must implement {nameof(IBulkCopyProvider)}.");
                var adapter = (IBulkCopyProvider)(Activator.CreateInstance(type)
                    ?? throw new InvalidOperationException($"Bulk-copy adapter '{provider.TypeName}' could not be constructed."));
                using var engine = adapter.Create(connectionUrl, dependencies);
                engine.Write(tableName, dataReader);
            });
        }

        public void Dispose() { }
    }
}
