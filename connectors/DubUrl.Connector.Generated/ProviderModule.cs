using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System.Data.Common;
using System.Reflection;

namespace DubUrl.Connector;

/// <summary>
/// Registers one connector described by immutable assembly metadata generated from the connector matrix.
/// </summary>
public sealed class ProviderModule : IDubUrlProviderModule
{
    private static readonly IReadOnlyDictionary<string, string> Metadata =
        typeof(ProviderModule).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value ?? string.Empty, StringComparer.Ordinal);

    public void Register(IProviderCatalogBuilder catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var aliases = Required("Aliases").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var dialectType = ResolveType("DialectType");
        var mapperType = ResolveType("MapperType");
        var parametrizerType = ResolveType("ParametrizerType");
        var factory = ResolveFactory();
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect(dialectType, aliases);
        var dialect = dialects.Build().Get(dialectType);

        catalog.AddProvider(new ProviderRegistration(
            Required("DatabaseName"),
            aliases,
            Required("ProviderInvariant"),
            () => factory,
            dialect,
            () => (IParametrizer)(Activator.CreateInstance(parametrizerType)
                ?? throw new InvalidOperationException($"Parametrizer '{parametrizerType.FullName}' could not be constructed.")),
            context => CreateMapper(mapperType, context))
        {
            BulkCopyProvider = mapperType.GetCustomAttribute<DubUrl.BulkCopy.BulkCopyProviderAttribute>(inherit: false)?.Create()
        });
    }

    private static DbProviderFactory ResolveFactory()
    {
        var type = ResolveType("FactoryType");
        var instance = type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
            ?? type.GetField("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        return instance as DbProviderFactory
            ?? throw new InvalidOperationException($"Factory '{type.FullName}' does not expose a public static DbProviderFactory Instance.");
    }

    private static IMapper CreateMapper(Type mapperType, ProviderCompilationContext context)
    {
        object[] parameters = typeof(IFileBasedMapper).IsAssignableFrom(mapperType)
            ? [context.ConnectionStringBuilder, context.Dialect, context.Parametrizer, context.RootPath]
            : [context.ConnectionStringBuilder, context.Dialect, context.Parametrizer];
        return Activator.CreateInstance(mapperType, parameters) as IMapper
            ?? throw new InvalidOperationException($"Mapper '{mapperType.FullName}' could not be constructed.");
    }

    private static Type ResolveType(string key)
        => Type.GetType(Required(key), throwOnError: true)
            ?? throw new TypeLoadException($"Connector type metadata '{key}' could not be resolved.");

    private static string Required(string key)
        => Metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Connector assembly metadata '{key}' is missing.");
}
