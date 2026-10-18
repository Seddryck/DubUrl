using DubUrl.Querying.Dialects;

namespace DubUrl.Schema;

public sealed class NativeTypeRegistry
{
    private readonly Dictionary<Type, Dictionary<string, string>> mappings = [];

    public static NativeTypeRegistry Default { get; } = new();

    public NativeTypeRegistry Register<TDialect>(params string[] typeNames) where TDialect : IDialect
    {
        if (!mappings.TryGetValue(typeof(TDialect), out var types))
            mappings.Add(typeof(TDialect), types = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        foreach (var typeName in typeNames)
        {
            var validated = new NativeDatabaseType(typeName);
            types[validated.Name] = validated.Name.ToUpperInvariant();
        }
        return this;
    }

    public bool TryResolve(IDialect dialect, NativeDatabaseType nativeType, out string resolvedType)
    {
        if (nativeType.Dialects.Count > 0 && !nativeType.Dialects.Any(type => type.IsInstanceOfType(dialect)))
        {
            resolvedType = string.Empty;
            return false;
        }

        foreach (var mapping in mappings)
            if (mapping.Key.IsInstanceOfType(dialect) && mapping.Value.TryGetValue(nativeType.Name, out resolvedType!))
                return true;

        if ((dialect.Capabilities ?? DialectCapabilities.Default).NativeTypes.Contains(nativeType.Name))
        {
            resolvedType = nativeType.Name.ToUpperInvariant();
            return true;
        }

        resolvedType = string.Empty;
        return false;
    }
}
