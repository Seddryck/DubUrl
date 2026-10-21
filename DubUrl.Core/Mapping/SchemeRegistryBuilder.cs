using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Core.ProviderPacks;

namespace DubUrl.Mapping;
/// <summary>Compatibility configuration facade over ProviderCatalogBuilder.</summary>
public class SchemeRegistryBuilder
{
    private static readonly DefaultRegistryCache DefaultCache = new(CreateDefaultCatalog);
    private string _rootPath = string.Empty;
    private readonly List<Assembly> _assemblies = [.. new AssemblyTypesProbe().Assemblies];
    private bool _autoDiscoverMappings;
    private readonly List<MapperInfo> _mapperInfos = [];
    private readonly List<string> _exclusions = [];

    public SchemeRegistryBuilder()
    { }

    /// <summary>
    /// Reuses a registry snapshot until the installed connector store changes.
    /// </summary>
    [RequiresUnreferencedCode("Default discovery loads provider modules and reflects over dialect attributes. Use ProviderCatalogBuilder with a custom explicit module for Native AOT.")]
    public static SchemeRegistry GetDefault()
        => DefaultCache.GetSchemes(new ProviderPackStore());

    /// <summary>Rebuilds the default registry, for example after loading application providers dynamically.</summary>
    public static SchemeRegistry RefreshDefault()
    {
        DefaultCache.Invalidate();
        return GetDefault();
    }

    private static ProviderCatalogBuilder CreateDefaultCatalog(ProviderPackStore store)
    {
        var builder = new ProviderCatalogBuilder();
        var diagnostics = new List<ProviderPackDiagnostic>();
        builder.AddInstalledPacks(store, diagnostics);
        if (diagnostics.Count != 0)
            throw new ProviderPackException("module-load-failed",
                string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.Message)));

        builder.AddApplicationProviders();
        return builder;
    }

    public static IDialectRegistry GetDefaultDialectRegistry(params string[] schemes)
    {
        var registry = (DialectRegistry)DefaultCache.GetDialects(new ProviderPackStore());

        if (schemes.Length == 0)
            return registry;

        var selectedDialects = new List<IDialect>();
        var unknownSchemes = new List<string>();
        foreach (var scheme in schemes)
        {
            try
            {
                selectedDialects.Add(registry.Get(scheme));
            }
            catch (DialectNotFoundException)
            {
                unknownSchemes.Add(scheme);
            }
        }
        if (unknownSchemes.Count > 0)
            throw new ArgumentException($"No dialect is mapped to: {string.Join(", ", unknownSchemes)}.", nameof(schemes));

        return registry.Select(selectedDialects.Select(dialect => dialect.GetType()));
    }

    public SchemeRegistryBuilder WithAssemblies(params Assembly[] assemblies)
    {
        _assemblies.AddRange(assemblies);
        return this;
    }

    public SchemeRegistryBuilder WithRootPath(string path)
    {
        _rootPath = path;
        return this;
    }

    public SchemeRegistryBuilder WithAutoDiscoveredMappings()
    {
        _autoDiscoverMappings = true;
        return this;
    }

    public SchemeRegistry Build()
    {
        var discovered = _autoDiscoverMappings
            ? AttributeProviderModule.Discover(_assemblies.Distinct().ToArray()) : [];
        var mappings = _mapperInfos.Union(discovered)
            .Where(info => !_exclusions.Any(alias => info.Aliases.Contains(alias, StringComparer.OrdinalIgnoreCase)))
            .ToArray();
        foreach (var info in mappings) CheckMapper(info);
        var catalog = new ProviderCatalogBuilder().WithRootPath(_rootPath);
        try
        {
            ProviderCatalogBuilder.RegisterMappings(catalog, mappings, GetProvider);
            return catalog.Build();
        }
        catch (ProviderRegistrationException exception)
        {
            // Preserve the compatibility API's duplicate-mapper exception without constructing mappers.
            var conflicts = mappings.Where(info => info.Aliases.Any(alias =>
                alias.Trim().Equals(exception.Alias, StringComparison.OrdinalIgnoreCase))).Take(2).ToArray();
            throw new MapperAlreadyExistingException(exception.Alias, conflicts[0].MapperType, conflicts[1].MapperType);
        }
    }

    #region Fluent Configuration for Mappings

    public SchemeRegistryBuilder AddMapping<M, D, P>(string databaseName, string alias, string providerName)
        where M : IMapper
        where D : IDialect
        where P : IParametrizer
        => AddMapping<M, D, P>(databaseName, new[] { alias }, providerName);

    public SchemeRegistryBuilder AddMapping<M, D, P>(string databaseName, string[] aliases, string providerName)
        where M : IMapper
        where D : IDialect
        where P : IParametrizer
    {
        _mapperInfos.Add(
            new MapperInfo(
                typeof(M), databaseName, aliases,
                typeof(D), 9, providerName, typeof(P),
                string.Empty, BrandAttribute.DefaultMainColor, BrandAttribute.DefaultSecondaryColor
            ));
        return this;
    }

    protected virtual void CheckMapper(MapperInfo info)
    {
        if (!info.MapperType.IsAssignableTo(typeof(IMapper)))
            throw new ArgumentException(nameof(info.MapperType));

        if (!info.DialectType.IsAssignableTo(typeof(IDialect)))
            throw new ArgumentException(nameof(info.DialectType));

        if (!info.ParametrizerType.IsAssignableTo(typeof(IParametrizer)))
            throw new ArgumentException(nameof(info.ParametrizerType));
    }

    public SchemeRegistryBuilder RemoveMapping(string alias)
    {
        alias = SchemeRegistryBuilder.GetAlias([alias]);
        if (_mapperInfos.Any(x => x.Aliases.Contains(alias)))
        {
            var info = _mapperInfos.First(x => x.Aliases.Contains(alias));
            _mapperInfos.Remove(info);
        }
        else
            _exclusions.Add(alias);
        return this;
    }

    public SchemeRegistryBuilder ReplaceMapper(Type oldMapper, Type newMapper)
    {
        var info = _mapperInfos.Single(x => x.MapperType == oldMapper);
        _mapperInfos[_mapperInfos.IndexOf(info)] = info with { MapperType = newMapper };
        return this;
    }

    public SchemeRegistryBuilder AddAlias(string alias, string original)
    {
        var info = _mapperInfos.Single(x => x.Aliases.Contains(original));
        _mapperInfos.Remove(info);
        info.Aliases = [.. info.Aliases, alias];
        _mapperInfos.Add(info);
        return this;
    }

    public SchemeRegistryBuilder ReplaceDriverLocatorFactory(Type mapper, DriverLocatorFactory factory)
        => throw new NotImplementedException();

    #endregion

    #region Static Helpers

    public static string GetAlias(string[] aliases)
        => GetAlias(new SchemeKey(aliases));

    public static string GetAlias(SchemeKey scheme)
    {
        var aliases = scheme.Parts;
        var main = aliases.Count == 1
            ? aliases[0]
            : aliases.Contains("oledb", StringComparer.OrdinalIgnoreCase) ? "oledb"
            : aliases.Contains("odbc", StringComparer.OrdinalIgnoreCase) ? "odbc"
            : throw new ArgumentOutOfRangeException(nameof(scheme));

        var second = aliases.FirstOrDefault(x => !x.Equals(main, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(second) ? main : $"{main}+{second}";
    }

    public static DbProviderFactory? GetProvider(string providerName)
    {
        DbProviderFactories.TryGetFactory(providerName, out var factory);
        return factory;
    }

    #endregion
}
