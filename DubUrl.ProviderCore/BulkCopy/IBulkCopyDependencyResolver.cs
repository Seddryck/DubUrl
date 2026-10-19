using System.Reflection;

namespace DubUrl.BulkCopy;

/// <summary>Resolves optional bulk-copy dependencies in the actual driver's load context.</summary>
public interface IBulkCopyDependencyResolver
{
    /// <summary>Enables lazy dependency resolution until the returned scope is disposed.</summary>
    IDisposable EnterScope(Assembly driverAssembly);

    /// <summary>Resolves an optional helper type without requiring a compile-time reference.</summary>
    Type ResolveType(Assembly driverAssembly, string assemblyName, string typeName);
}
