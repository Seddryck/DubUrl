using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Dialect;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;

namespace DubUrl.Mapping;
public class SchemeRegistryBuilder
{
    private string _rootPath = string.Empty;
    private readonly List<Assembly> _assemblies = [typeof(SchemeRegistryBuilder).Assembly];
    private readonly List<Type> _mapperIntrospectors = [];
    private readonly List<MapperInfo> _mapperInfos = [];
    private readonly List<string> _exclusions = [];

    public SchemeRegistryBuilder()
    { }

    public static SchemeRegistry GetDefault()
        => new SchemeRegistryBuilder()
            .WithRootPath(string.Empty)
            .WithAssemblies(typeof(SchemeRegistryBuilder).Assembly)
            .WithAutoDiscoveredMappings()
            .Build();

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
        _mapperIntrospectors.Add(typeof(NativeMapperIntrospector));
        _mapperIntrospectors.Add(typeof(WrapperMapperIntrospector));
        return this;
    }

    public SchemeRegistry Build()
    {
        var probe = new AssemblyTypesProbe([.. _assemblies.Distinct()]);
        var introspectors = _mapperIntrospectors
            .Select(x => x.GetConstructor([typeof(AssemblyTypesProbe)])!)
            .Select(ctor => ctor.Invoke([probe]))
            .Cast<BaseMapperIntrospector>()
            .ToArray();

        var mappingData = new List<MapperInfo>();

        var infos = introspectors.SelectMany(i => i.Locate()) ?? [];
        foreach (var info in _mapperInfos.Union(infos))
        {
            if (_exclusions.Any(x => info.Aliases.Contains(x)))
                continue;

            CheckMapper(info);

            if (mappingData.Contains(info))
                throw new ArgumentException($"Duplicate mapper: {info.MapperType.Name}");

            mappingData.Add(info);
        }

        var mapperDict = new Dictionary<string, IMapper>();

        var dialectRegistryBuilder = new DialectRegistryBuilder();
        foreach (var info in mappingData)
            dialectRegistryBuilder.AddDialect(info.DialectType, [.. info.Aliases]);

        var dialectRegistry = dialectRegistryBuilder.Build();

        foreach (var info in mappingData)
        {
            var provider = GetProvider(info.ProviderInvariantName);
            if (provider == null)
            {
                Debug.WriteLine($"Skipping mapper for '{info.ProviderInvariantName}' (no provider found).");
                continue;
            }

            var ctorParams = new List<object>
            {
                provider.CreateConnectionStringBuilder()!,
                dialectRegistry.Get(info.Aliases.First()),
                ParametrizerFactory.Instantiate(info.ParametrizerType)
            };

            var paramTypes = new List<Type>
            {
                typeof(DbConnectionStringBuilder),
                typeof(IDialect),
                typeof(IParametrizer)
            };

            if (typeof(IFileBasedMapper).IsAssignableFrom(info.MapperType))
            {
                paramTypes.Add(typeof(string));
                ctorParams.Add(_rootPath);
            }

            var ctor = info.MapperType.GetConstructor(BindingFlags.Instance | BindingFlags.Public, paramTypes.ToArray())
                       ?? throw new NullReferenceException($"Missing constructor on {info.MapperType.Name}");

            var mapper = (IMapper)ctor.Invoke(ctorParams.ToArray());

            foreach (var alias in info.Aliases)
            {
                if (!mapperDict.TryAdd(alias, mapper))
                    throw new MapperAlreadyExistingException(alias, mapperDict[alias], mapper);
            }
        }

        return new SchemeRegistry(mapperDict);
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
        info.MapperType = newMapper;
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
    {
        var main = aliases.Length == 1
            ? aliases[0]
            : aliases.Contains("oledb") ? "oledb"
            : aliases.Contains("odbc") ? "odbc"
            : throw new ArgumentOutOfRangeException(nameof(aliases));

        var second = aliases.FirstOrDefault(x => x != main);
        return string.IsNullOrEmpty(second) ? main : $"{main}+{second}";
    }

    public static DbProviderFactory? GetProvider(string providerName)
    {
        DbProviderFactories.TryGetFactory(providerName, out var factory);
        return factory;
    }

    #endregion
}
