using System.Data.Common;
using DubUrl.Locating.Options;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DubUrl.Core.ProviderPacks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Mapping;

/// <summary>
/// Creates scheme registries from explicitly registered provider modules.
/// </summary>
public sealed class ProviderCatalogBuilder : IProviderCatalogBuilder
{
    private readonly List<ProviderRegistration> _providers = [];
    private readonly Dictionary<string, ProviderRegistration> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Type> _odbcLocatorOptions = [];
    private string _rootPath = string.Empty;

    public ProviderCatalogBuilder WithRootPath(string rootPath)
    {
        _rootPath = rootPath ?? throw new ArgumentNullException(nameof(rootPath));
        return this;
    }

    public ProviderCatalogBuilder AddModule(IDubUrlProviderModule module)
    {
        ArgumentNullException.ThrowIfNull(module);
        module.Register(this);
        return this;
    }

    public ProviderCatalogBuilder AddInstalledPacks(
        ProviderPackStore store,
        ICollection<ProviderPackDiagnostic>? diagnostics = null)
    {
        var loader = new InstalledProviderModuleLoader();
        foreach (var module in loader.LoadActive(store, diagnostics))
            AddModule(module);
        return this;
    }

    /// <summary>Discovers application provider dependencies, preserving explicitly registered and installed aliases.</summary>
    public ProviderCatalogBuilder AddApplicationProviders()
    {
        var assemblies = ApplicationProviderDiscovery.GetAssemblies();
        if (assemblies.Length != 0)
            AddFallbackModule(AttributeProviderModule.FromApplication(assemblies));
        return this;
    }

    public IProviderCatalogBuilder AddProvider(ProviderRegistration registration)
        => AddProvider(registration, fallback: false);

    internal ProviderCatalogBuilder AddFallbackModule(IDubUrlProviderModule module)
    {
        module.Register(new FallbackCatalog(this));
        return this;
    }

    private IProviderCatalogBuilder AddProvider(ProviderRegistration registration, bool fallback)
    {
        ArgumentNullException.ThrowIfNull(registration);

        foreach (var alias in registration.Aliases)
        {
            if (!fallback && _aliases.TryGetValue(alias, out var existing))
                throw new ProviderRegistrationException(alias, existing.Name, registration.Name);
        }

        _providers.Add(registration);
        foreach (var alias in registration.Aliases)
            _aliases.TryAdd(alias, registration);
        return this;
    }

    public IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum
    {
        _odbcLocatorOptions.Add(typeof(TOption));
        return this;
    }

    public SchemeRegistry Build()
    {
        var registrations = new Dictionary<string, SchemeRegistration>(StringComparer.OrdinalIgnoreCase);
        var optionTypes = _odbcLocatorOptions.OrderBy(type => type.FullName, StringComparer.Ordinal).ToArray();
        var rootPath = _rootPath;

        foreach (var provider in _providers)
        {
            var aliases = GetEffectiveAliases(provider);
            if (aliases.Length == 0)
                continue;
            var providerFactory = provider.ProviderFactory();
            if (providerFactory is null)
            {
                Debug.WriteLine($"Skipping provider '{provider.Name}' because '{provider.ProviderInvariantName}' is not registered.");
                continue;
            }

            IMapper CreateMapper()
                => provider.Compile(new ProviderCompilationContext(
                    providerFactory,
                    provider.Dialect,
                    provider.ParametrizerFactory(),
                    rootPath,
                    optionTypes));

            var schemeRegistration = new SchemeRegistration(
                CreateMapper,
                _ => providerFactory,
                provider.Capabilities,
                provider.BulkCopyProvider);

            foreach (var alias in aliases)
                registrations.Add(alias, schemeRegistration);
        }

        return SchemeRegistry.FromRegistrations(registrations);
    }

    public IDialectRegistry BuildDialects()
    {
        var active = _providers.Where(provider => GetEffectiveAliases(provider).Length != 0).ToArray();
        return new DialectRegistry(
            active.GroupBy(provider => provider.Dialect.GetType()).ToDictionary(group => group.Key, group => group.First().Dialect),
            active.GroupBy(provider => provider.Dialect.GetType()).ToDictionary(group => group.Key,
                group => group.SelectMany(GetEffectiveAliases).Distinct().ToList()));
    }

    private string[] GetEffectiveAliases(ProviderRegistration provider)
        => provider.Aliases.Where(alias => ReferenceEquals(_aliases[alias], provider)).ToArray();

    internal static DbProviderFactory? ResolveRegisteredFactory(string invariant)
        => DbProviderFactories.TryGetFactory(invariant, out var factory) ? factory : null;

    // Adapt the public mapper-delegate constructor without invoking user code eagerly.
    internal static Dictionary<string, SchemeRegistration> AdaptMapperFactories(Dictionary<string, Func<IMapper>> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        return factories.ToDictionary(pair => pair.Key, pair =>
        {
            DbProviderFactory ResolveFactory(IMapper? mapper)
            {
                var invariant = (mapper ?? pair.Value()).GetProviderName();
                return ProviderCatalogBuilder.ResolveRegisteredFactory(invariant)
                    ?? throw new ProviderNotFoundException(invariant, DbProviderFactories.GetProviderInvariantNames().ToArray());
            }
            return new SchemeRegistration(pair.Value, ResolveFactory, new Dictionary<Type, object>(), null);
        }, StringComparer.OrdinalIgnoreCase);
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
            var registration = CreateRegistration(info, dialects, resolveFactory);
            catalog.AddProvider(registration);
        }
        if (infos.Any(info => info.ProviderInvariantName == "System.Data.Odbc"))
        {
            catalog.AddOdbcLocatorOption<ArchitectureOption>();
            catalog.AddOdbcLocatorOption<EncodingOption>();
            catalog.AddOdbcLocatorOption<VersionOption>();
        }
    }

    private static ProviderRegistration CreateRegistration(MapperInfo info, IDialectRegistry dialects, Func<string, DbProviderFactory?> resolveFactory)
    {
        var fileBased = typeof(IFileBasedMapper).IsAssignableFrom(info.MapperType);
        Type[] parameters = [typeof(DbConnectionStringBuilder), typeof(IDialect), typeof(IParametrizer)];
        if (fileBased) parameters = [.. parameters, typeof(string)];
        var optionsConstructor = info.MapperType.GetConstructor([.. parameters, typeof(IEnumerable<Type>)]);
        var constructor = optionsConstructor ?? info.MapperType.GetConstructor(parameters)
            ?? throw new InvalidOperationException($"Missing mapper constructor on '{info.MapperType.FullName}'.");
        var parametrizer = info.ParametrizerType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException($"Missing parametrizer constructor on '{info.ParametrizerType.FullName}'.");
        var registration = new ProviderRegistration(info.DatabaseName, info.Aliases, info.ProviderInvariantName,
            () => resolveFactory(info.ProviderInvariantName), dialects.Get(info.DialectType),
            () => (IParametrizer)parametrizer.Invoke(null), context =>
            {
                object[] arguments = [context.ConnectionStringBuilder, context.Dialect, context.Parametrizer];
                if (fileBased) arguments = [.. arguments, context.RootPath];
                if (optionsConstructor is not null) arguments = [.. arguments, context.OdbcLocatorOptions];
                var mapper = (IMapper)constructor.Invoke(arguments);
                if (mapper is BaseMapper baseMapper)
                    baseMapper.SetProviderMetadata(info.ProviderInvariantName, info.Database is null ? null : info.ConnectivityType);
                return mapper;
            })
        { BulkCopyProvider = info.BulkCopyProvider };
        if (info.SchemaDiscovery is not null) registration.WithCapabilities(info.SchemaDiscovery);
        if (info.ConnectionConfiguration is not null) registration.WithCapabilities(info.ConnectionConfiguration);
        if (info.ReadOnlyConnection is not null) registration.WithCapabilities(info.ReadOnlyConnection);
        return registration;
    }

    private sealed class FallbackCatalog(ProviderCatalogBuilder owner) : IProviderCatalogBuilder
    {
        public IProviderCatalogBuilder AddProvider(ProviderRegistration registration)
        {
            owner.AddProvider(registration, fallback: true);
            return this;
        }

        public IProviderCatalogBuilder AddOdbcLocatorOption<TOption>() where TOption : struct, Enum
        {
            owner.AddOdbcLocatorOption<TOption>();
            return this;
        }
    }
}

internal sealed record SchemeRegistration(
    Func<IMapper> MapperFactory,
    Func<IMapper?, DbProviderFactory> ProviderFactory,
    IReadOnlyDictionary<Type, object> Capabilities,
    DubUrl.BulkCopy.IBulkCopyProvider? BulkCopyProvider);
