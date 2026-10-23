using DubUrl.Core.ProviderPacks;
using System.Reflection;
using System.Runtime.Loader;

namespace DubUrl.Mapping;

/// <summary>
/// Loads active, verified connectors by reading their implementation attributes.
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
            ProviderPackLoadContext? context = null;
            try
            {
                var assemblyPath = Path.GetFullPath(Path.Combine(pack.InstallPath, pack.Manifest.Assembly));
                context = new ProviderPackLoadContext(assemblyPath, pack.InstallPath);
                var module = AttributeProviderModule.FromConnector(pack.Manifest, context.LoadConnectorAssembly);
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
                context?.Unload();
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

    private sealed class ProviderPackLoadContext(string componentAssemblyPath, string installPath) : AssemblyLoadContext(isCollectible: true)
    {
        private static readonly HashSet<string> SharedAssemblies = GetSharedAssemblies();
        private readonly AssemblyDependencyResolver _resolver = new(componentAssemblyPath);
        private readonly string root = installPath;
        private readonly Dictionary<string, Assembly> components = new(StringComparer.OrdinalIgnoreCase);

        public Assembly LoadConnectorAssembly(string relativePath)
        {
            if (components.TryGetValue(relativePath, out var assembly)) return assembly;
            // Paths were validated by the store before the connector became active.
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            var name = AssemblyName.GetAssemblyName(path);
            var existing = Assemblies.SingleOrDefault(candidate => AssemblyName.ReferenceMatchesDefinition(candidate.GetName(), name));
            if (existing is not null) return components[relativePath] = existing;
            var resolved = _resolver.ResolveAssemblyToPath(name) ?? path;
            using var stream = File.OpenRead(resolved);
            return components[relativePath] = LoadFromStream(stream);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name is not null && SharedAssemblies.Contains(assemblyName.Name))
                return Default.LoadFromAssemblyName(assemblyName);
            var sharedApi = Default.Assemblies.Where(assembly => !assembly.IsDynamic)
                .SelectMany(assembly => assembly.GetCustomAttributes<SharedProviderDependencyAttribute>())
                .Select(attribute => attribute.ContractType.Assembly)
                .Distinct()
                .SingleOrDefault(assembly => assembly.GetName().FullName == assemblyName.FullName);
            if (sharedApi is not null)
                return sharedApi;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            if (path is null) return null;
            using var stream = File.OpenRead(path);
            return LoadFromStream(stream);
        }

        private static HashSet<string> GetSharedAssemblies()
        {
            using var stream = typeof(InstalledProviderModuleLoader).Assembly
                .GetManifestResourceStream("DubUrl.Core.SharedDependencies")
                ?? throw new InvalidOperationException("Core's shared dependency inventory is missing.");
            using var reader = new StreamReader(stream);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { typeof(InstalledProviderModuleLoader).Assembly.GetName().Name! };
            while (reader.ReadLine() is { } path)
                if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    names.Add(Path.GetFileNameWithoutExtension(path.Replace('\\', '/')));
            return names;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
