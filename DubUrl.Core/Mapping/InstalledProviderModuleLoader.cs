using DubUrl.Core.ProviderPacks;
using System.Reflection;
using System.Runtime.Loader;

namespace DubUrl.Mapping;

/// <summary>
/// Loads active, verified provider packs through the explicit provider-module contract.
/// </summary>
public sealed class InstalledProviderModuleLoader : IDisposable
{
    private readonly List<AssemblyLoadContext> _contexts = [];

    public IReadOnlyList<IDubUrlProviderModule> LoadActive(
        ProviderPackStore store,
        ICollection<ProviderPackDiagnostic>? diagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var modules = new List<IDubUrlProviderModule>();
        foreach (var pack in store.Inspect().Where(pack => pack.Status == ProviderPackStatus.Active))
        {
            try
            {
                var assemblyPath = Path.GetFullPath(Path.Combine(pack.InstallPath, pack.Manifest.Module.Assembly));
                var context = new ProviderPackLoadContext(assemblyPath);
                using var assemblyStream = File.OpenRead(assemblyPath);
                var assembly = context.LoadFromStream(assemblyStream);
                var type = assembly.GetType(pack.Manifest.Module.Type, throwOnError: true, ignoreCase: false)
                    ?? throw new TypeLoadException($"Module type '{pack.Manifest.Module.Type}' was not found.");
                if (!typeof(IDubUrlProviderModule).IsAssignableFrom(type))
                    throw new InvalidOperationException($"Module type '{type.FullName}' does not implement {nameof(IDubUrlProviderModule)}.");
                var module = Activator.CreateInstance(type) as IDubUrlProviderModule
                    ?? throw new InvalidOperationException($"Module type '{type.FullName}' could not be constructed.");
                _contexts.Add(context);
                modules.Add(module);
            }
            catch (Exception exception) when (exception is IOException
                or BadImageFormatException
                or FileLoadException
                or FileNotFoundException
                or TypeLoadException
                or MissingMethodException
                or TargetInvocationException
                or InvalidOperationException)
            {
                diagnostics?.Add(new ProviderPackDiagnostic(
                    "module-load-failed",
                    $"Provider '{pack.Manifest.Provider}' could not be loaded: {exception.Message}"));
            }
        }
        return modules;
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Unload();
        _contexts.Clear();
    }

    private sealed class ProviderPackLoadContext(string componentAssemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver _resolver = new(componentAssemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is "DubUrl" or "DubUrl.Core")
                return null;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
