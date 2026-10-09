using DubUrl.Querying.Dialects;

namespace DubUrl.Schema;

public sealed class NativeTypeRegistry
{
    private readonly Dictionary<Type, Dictionary<string, string>> mappings = [];

    public static NativeTypeRegistry Default { get; } = CreateDefault();

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

        resolvedType = string.Empty;
        return false;
    }

    private static NativeTypeRegistry CreateDefault()
        => new NativeTypeRegistry()
            .Register<PgsqlDialect>("JSON", "JSONB", "UUID", "TEXT", "CITEXT")
            .Register<CockRoachDialect>("JSON", "JSONB", "UUID")
            .Register<CrateDbDialect>("JSON", "OBJECT", "GEO_POINT", "GEO_SHAPE")
            .Register<MySqlDialect>("JSON", "GEOMETRY")
            .Register<SingleStoreDialect>("JSON", "GEOGRAPHYPOINT", "GEOGRAPHY")
            .Register<TSqlDialect>("XML", "UNIQUEIDENTIFIER", "GEOGRAPHY", "GEOMETRY")
            .Register<DuckDbDialect>("JSON", "UUID", "HUGEINT")
            .Register<SqliteDialect>("TEXT", "BLOB", "INTEGER", "REAL", "NUMERIC");
}
