using System.Reflection;
using System.Runtime.Loader;

namespace DubUrl.BulkCopy;

/// <summary>Loads optional managed DLLs lazily, using the driver's context and explicit search directories.</summary>
/// <remarks>No assemblies are loaded during construction. Native dependencies remain the driver's responsibility.</remarks>
public sealed class BulkCopyDependencyResolver : IBulkCopyDependencyResolver
{
    public static BulkCopyDependencyResolver Default { get; } = new();
    private readonly string[] searchDirectories;

    public BulkCopyDependencyResolver(IEnumerable<string>? searchDirectories = null)
        => this.searchDirectories = searchDirectories?.Select(Path.GetFullPath).Distinct().ToArray() ?? [];

    public IDisposable EnterScope(Assembly driverAssembly)
    {
        ArgumentNullException.ThrowIfNull(driverAssembly);
        var context = AssemblyLoadContext.GetLoadContext(driverAssembly) ?? AssemblyLoadContext.Default;
        var driverDirectory = string.IsNullOrEmpty(driverAssembly.Location) ? null : Path.GetDirectoryName(driverAssembly.Location);
        string[] directories = driverDirectory is null ? searchDirectories : [driverDirectory, .. searchDirectories];
        return new ResolutionScope(context, directories);
    }

    public Type ResolveType(Assembly driverAssembly, string assemblyName, string typeName)
    {
        ArgumentNullException.ThrowIfNull(driverAssembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        ArgumentException.ThrowIfNullOrWhiteSpace(typeName);
        Type? type = null;
        this.Execute(driverAssembly, () =>
        {
            var context = AssemblyLoadContext.GetLoadContext(driverAssembly) ?? AssemblyLoadContext.Default;
            var assembly = context.LoadFromAssemblyName(new AssemblyName(assemblyName));
            type = assembly.GetType(typeName, throwOnError: true)!;
        });
        return type!;
    }

    private sealed class ResolutionScope : IDisposable
    {
        private readonly AssemblyLoadContext context;
        private readonly string[] directories;
        private bool disposed;

        public ResolutionScope(AssemblyLoadContext context, string[] directories)
        {
            this.context = context;
            this.directories = directories;
            context.Resolving += Resolve;
        }

        private Assembly? Resolve(AssemblyLoadContext loadContext, AssemblyName name)
        {
            if (string.IsNullOrEmpty(name.Name))
                return null;
            // Allow resource fallback to proceed without turning a missing satellite into a failure.
            if (name.Name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase))
                return null;
            foreach (var directory in directories)
            {
                var path = Path.Combine(directory, name.Name + ".dll");
                if (File.Exists(path) && AssemblyName.ReferenceMatchesDefinition(name, AssemblyName.GetAssemblyName(path)))
                    return loadContext.LoadFromAssemblyPath(path);
            }

            // Other active scopes or host handlers may know about this dependency.
            return null;
        }

        public void Dispose()
        {
            if (disposed)
                return;
            context.Resolving -= Resolve;
            disposed = true;
        }
    }
}
