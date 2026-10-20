using System.Data.Common;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using DubUrl.Registering;

namespace DubUrl.Mapping;

/// <summary>Finds providers in the application's resolved runtime dependencies, without a catalog or archive.</summary>
internal static class ApplicationProviderDiscovery
{
    private static readonly Lazy<string[]> RuntimeNames = new(ReadRuntimeAssemblyNames);
    internal static Assembly[] GetAssemblies()
    {
        // TPA includes NuGet runtime assets even when no application code has referenced their types yet.
        var names = RuntimeAssemblyNames().Where(name => name.StartsWith("DubUrl.Provider.", StringComparison.Ordinal));
        var loaded = AssemblyLoadContext.Default.Assemblies
            .Where(assembly => assembly.GetName().Name?.StartsWith("DubUrl.Provider.", StringComparison.Ordinal) == true);
        return loaded.Concat(names.Select(name => AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name))))
            .Distinct().OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal).ToArray();
    }

    internal static DbProviderFactory? ResolveFactory(string invariant)
    {
        if (DbProviderFactories.TryGetFactory(invariant, out var registered))
            return registered;

        // Some drivers use an assembly suffix (for example Teradata.Client.Provider).
        var candidates = RuntimeAssemblyNames().Where(name => name.Equals(invariant, StringComparison.OrdinalIgnoreCase)
            || name.StartsWith(invariant + ".", StringComparison.OrdinalIgnoreCase));
        foreach (var name in candidates.Prepend(invariant).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Assembly assembly;
            try { assembly = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(name)); }
            catch (FileNotFoundException) { continue; }
            var factories = ProviderFactoriesRegistrator.FindFactoryTypes(assembly).ToArray();
            if (factories.Length == 1)
                return ProviderFactoriesRegistrator.GetFactoryInstance(factories[0]);
        }
        return null;
    }

    private static IEnumerable<string> RuntimeAssemblyNames()
        => RuntimeNames.Value;

    private static string[] ReadRuntimeAssemblyNames()
    {
        var names = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.GetFileNameWithoutExtension(path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entry = Assembly.GetEntryAssembly();
        if (entry is not null)
        {
            // Single-file apps keep their standard dependency metadata beside the executable.
            var path = Path.Combine(AppContext.BaseDirectory, entry.GetName().Name + ".deps.json");
            if (File.Exists(path))
            {
                using var stream = File.OpenRead(path);
                using var document = JsonDocument.Parse(stream);
                foreach (var target in document.RootElement.GetProperty("targets").EnumerateObject())
                    foreach (var library in target.Value.EnumerateObject())
                    {
                        if (library.Value.TryGetProperty("runtime", out var runtime))
                            foreach (var asset in runtime.EnumerateObject())
                                if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                    names.Add(Path.GetFileNameWithoutExtension(asset.Name));
                        if (library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets))
                            foreach (var asset in runtimeTargets.EnumerateObject())
                                if (asset.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                                    && asset.Value.GetProperty("assetType").GetString() == "runtime")
                                    names.Add(Path.GetFileNameWithoutExtension(asset.Name));
                    }
            }
        }
        return names.Order(StringComparer.Ordinal).ToArray();
    }
}
