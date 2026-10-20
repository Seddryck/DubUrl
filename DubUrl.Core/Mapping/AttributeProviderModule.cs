using System.Data.Common;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;
using DubUrl.BulkCopy;
using DubUrl.Core.ProviderPacks;
using DubUrl.Registering;
using DubUrl.Locating.Options;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Mapping;

/// <summary>Reads provider attributes when registering a module, retaining the resolved constructors.</summary>
public sealed class AttributeProviderModule : IDubUrlProviderModule
{
    private readonly Assembly[] assemblies;
    private readonly MapperInfo[]? mappings;
    private readonly Func<string, DbProviderFactory?> resolveFactory = SchemeRegistryBuilder.GetProvider;

    private AttributeProviderModule(MapperInfo mapping, Func<string, DbProviderFactory?> factory)
        => (assemblies, mappings, resolveFactory) = ([], [mapping], factory);

    public static AttributeProviderModule FromConnector(ConnectorDefinition definition, Func<string, Assembly> loadAssembly)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(loadAssembly);
        var mapping = SelectMapping(Discover(definition.ImplementationAssemblies.Select(loadAssembly).ToArray()),
            definition.DatabaseName!, definition.Driver);
        if (!mapping.Aliases.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(definition.Schemes))
            throw new InvalidOperationException($"Connector '{definition.Provider}' schemes do not match its implementation attributes.");
        var factory = new Lazy<DbProviderFactory>(() => ProviderFactoriesRegistrator.GetFactoryInstance(
            ProviderFactoriesRegistrator.FindFactoryTypes(loadAssembly(definition.DriverAssemblyPath)).Single()));
        return new AttributeProviderModule(mapping, _ => factory.Value);
    }

    public AttributeProviderModule(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        this.assemblies = assemblies.Distinct().ToArray();
        if (this.assemblies.Length == 0) throw new ArgumentException("At least one assembly is required.", nameof(assemblies));
    }

    [RequiresUnreferencedCode("Provider discovery reads attributes and mapper constructors.")]
    public void Register(IProviderCatalogBuilder catalog)
        => RegisterMappings(catalog, mappings ?? Discover(assemblies), resolveFactory);

    public static MapperInfo[] Discover(params Assembly[] assemblies)
        => Discover(new AssemblyTypesProbe(assemblies));

    public static MapperInfo[] Discover(ITypesProbe types)
    {
        var probe = new AttributeTypesProbe(types.Locate());
        var native = new NativeMapperIntrospector(probe);
        return native.Locate().Concat(new WrapperMapperIntrospector(probe).Locate())
            .GroupBy(info => (info.MapperType, info.DatabaseName, info.ProviderInvariantName, info.DialectType, info.ParametrizerType))
            .Select(group => group.First() with
            {
                Aliases = group.SelectMany(info => info.Aliases).Distinct().ToArray(),
                BulkCopyProvider = probe.Attributes(group.Key.MapperType).OfType<BulkCopyProviderAttribute>().SingleOrDefault()?.Create()
            }).ToArray();
    }

    public static MapperInfo SelectMapping(IEnumerable<MapperInfo> mappings, string database, string invariant)
    {
        var candidates = mappings.Where(info => info.ProviderInvariantName == invariant
            && (info.DatabaseName.Equals(database, StringComparison.OrdinalIgnoreCase)
                || info.DatabaseName.EndsWith(" for " + database, StringComparison.OrdinalIgnoreCase))).ToArray();
        return candidates.Length == 1 ? candidates[0]
            : throw new InvalidOperationException($"Expected one attributed mapper for '{database}' using '{invariant}', found {candidates.Length}.");
    }

    internal static void RegisterMappings(IProviderCatalogBuilder catalog, IEnumerable<MapperInfo> mappings,
        Func<string, DbProviderFactory?> resolveFactory)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var infos = mappings.ToArray();
        var builder = new DialectRegistryBuilder();
        foreach (var info in infos) builder.AddDialect(info.DialectType, info.Aliases);
        var dialects = builder.Build();
        foreach (var info in infos)
        {
            var fileBased = typeof(IFileBasedMapper).IsAssignableFrom(info.MapperType);
            Type[] parameters = [typeof(DbConnectionStringBuilder), typeof(IDialect), typeof(IParametrizer)];
            if (fileBased) parameters = [.. parameters, typeof(string)];
            var optionsConstructor = info.MapperType.GetConstructor([.. parameters, typeof(IEnumerable<Type>)]);
            var constructor = optionsConstructor ?? info.MapperType.GetConstructor(parameters)
                ?? throw new InvalidOperationException($"Missing mapper constructor on '{info.MapperType.FullName}'.");
            var parametrizer = info.ParametrizerType.GetConstructor(Type.EmptyTypes)
                ?? throw new InvalidOperationException($"Missing parametrizer constructor on '{info.ParametrizerType.FullName}'.");
            catalog.AddProvider(new ProviderRegistration(info.DatabaseName, info.Aliases, info.ProviderInvariantName,
                () => resolveFactory(info.ProviderInvariantName), dialects.Get(info.DialectType),
                () => (IParametrizer)parametrizer.Invoke(null), context =>
                {
                    object[] arguments = [context.ConnectionStringBuilder, context.Dialect, context.Parametrizer];
                    if (fileBased) arguments = [.. arguments, context.RootPath];
                    if (optionsConstructor is not null) arguments = [.. arguments, context.OdbcLocatorOptions];
                    var mapper = (IMapper)constructor.Invoke(arguments);
                    if (mapper is BaseMapper baseMapper) baseMapper.SetProviderMetadata(info.ProviderInvariantName, info.ConnectivityType);
                    return mapper;
                }) { BulkCopyProvider = info.BulkCopyProvider });
        }
        if (infos.Any(info => info.ProviderInvariantName == "System.Data.Odbc"))
        {
            catalog.AddOdbcLocatorOption<ArchitectureOption>();
            catalog.AddOdbcLocatorOption<EncodingOption>();
            catalog.AddOdbcLocatorOption<VersionOption>();
        }
    }
}
