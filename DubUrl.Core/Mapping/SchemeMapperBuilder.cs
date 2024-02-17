using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public class SchemeMapperBuilder
{
    protected readonly char[] Separators = ['+', ':'];

    private readonly record struct ProviderInfo(string ProviderName, List<string> Aliases, Type DialectType, DriverLocatorFactory? DriverLocatorFactory);
    private bool IsBuilt { get; set; } = false;

    private List<MapperInfo> MapperData { get; } = [];
    protected Dictionary<string, IMapper> Mappers { get; set; } = [];
    protected Dictionary<string, IWrapperConnectivity> Wrappers { get; set; } = [];
    private BaseMapperIntrospector[] MapperIntrospectors { get; } = [new NativeMapperIntrospector(), new WrapperMapperIntrospector()];
    private DialectBuilder DialectBuilder { get; } = new();

    public SchemeMapperBuilder()
     : this([typeof(SchemeMapperBuilder).Assembly]) { }

    public SchemeMapperBuilder(Assembly[] assemblies)
    {
        var asmTypesProbe = new AssemblyTypesProbe(assemblies);
        MapperIntrospectors =
        [
            new NativeMapperIntrospector(asmTypesProbe)
            , new WrapperMapperIntrospector(asmTypesProbe)
        ];
        Initialize();
    }

    protected virtual void Initialize()
    {
        Initialize(MapperIntrospectors.Aggregate(
                Array.Empty<MapperInfo>(), (data, introspector)
                => [.. data, .. introspector.Locate()]));
    }

    protected internal virtual void Initialize(IEnumerable<MapperInfo> infos)
    {
        foreach (var mapperData in infos)
            AddMapping(mapperData);
    }

    public bool CanHandle(string scheme)
    {
        var alias = GetAlias(scheme.Split(Separators));
        return Mappers.ContainsKey(alias);
    }

    public virtual void Build()
    {
        foreach (var mapperData in MapperData)
            DialectBuilder.AddAliases(mapperData.DialectType, [.. mapperData.Aliases]);
        DialectBuilder.Build();

        Mappers.Clear();
        foreach (var mapperData in MapperData)
        {
            var provider = GetProvider(mapperData.ProviderInvariantName);
            if (provider == null)
            {
                Debug.WriteLine($"No provider registered with the name '{mapperData.ProviderInvariantName}', skipping associated mapper.");
                continue;
            }

            var ctorParamTypes = new List<Type>() { typeof(DbConnectionStringBuilder), typeof(IDialect), typeof(IParametrizer) };
            var ctorParams = new List<object>() {
                provider.CreateConnectionStringBuilder() ?? throw new NullReferenceException()
                , DialectBuilder.Get(mapperData.Aliases.First())
                , ParametrizerFactory.Instantiate(mapperData.ParametrizerType)
            };

            //if (mapperData.DriverLocatorFactory != null)
            //{
            //    ctorParamTypes.Add(typeof(DriverLocatorFactory));
            //    ctorParams.Add(mapperData.Value.DriverLocatorFactory);
            //}

            var ctor = mapperData.MapperType.GetConstructor(
                            BindingFlags.Instance | BindingFlags.Public,
                            [.. ctorParamTypes]
                        ) ?? throw new NullReferenceException($"Unable to find a suitable constructor for the mapper of type '{mapperData.MapperType.Name}'.");
            var mapper = ctor.Invoke([.. ctorParams]) as IMapper
                    ?? throw new NullReferenceException();

            foreach (var alias in mapperData.Aliases)
                if (!Mappers.TryAdd(alias, mapper))
                    throw new MapperAlreadyExistingException(alias, Mappers[alias], mapper);
        }

        Wrappers = Mappers.Select(x => x.Value.GetConnectivity())
                    .Where(x => x is IWrapperConnectivity)
                    .Cast<IWrapperConnectivity>()
                    .Distinct(new WrapperConnectivityComparer())
                    .ToDictionary(x => x.Alias);

        IsBuilt = true;
    }

    private string GetAlias(string[] aliases)
    {
        var wrapperAlias = Wrappers.SingleOrDefault(x => aliases.Contains(x.Key)).Value?.Alias;
        if (aliases.Length > 1 && string.IsNullOrEmpty(wrapperAlias))
            throw new ArgumentOutOfRangeException(nameof(aliases));

        var databaseAlias = aliases.SkipWhile(x => x.Equals(wrapperAlias)).First();

        return string.IsNullOrEmpty(wrapperAlias) ? databaseAlias : $"{wrapperAlias}+{databaseAlias}";
    }

    public IMapper GetMapper(string alias)
        => GetMapper(alias.Split(Separators));

    public virtual IMapper GetMapper(string[] aliases)
    {
        if (!IsBuilt)
            throw new InvalidOperationException();

        var alias = GetAlias(aliases);

        if (!Mappers.TryGetValue(alias, out var mapper))
            throw new SchemeNotFoundException(alias, [.. Mappers.Keys]);

        return mapper;
    }

    public virtual DbProviderFactory GetProviderFactory(string[] aliases)
    {
        if (!IsBuilt)
            throw new InvalidOperationException();

        var alias = GetAlias(aliases);

        if (!Mappers.ContainsKey(alias))
            throw new SchemeNotFoundException(alias, [.. Mappers.Keys]);

        var mapper = Mappers[alias];
        return GetProvider(mapper.GetProviderName())
            ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
    }

    protected internal static DbProviderFactory? GetProvider(string providerName)
    {
        DbProviderFactories.TryGetFactory(providerName, out var providerFactory);
        return providerFactory;
    }

    #region Add, remove aliases and mappings

    public void AddAlias(string alias, string original)
    {
        var mapperData = MapperData.Single(x => x.Aliases.Contains(original));
        var newAliases = mapperData.Aliases.ToList();
        newAliases.Add(alias);
        mapperData.Aliases = [.. newAliases];
    }

    public void AddMapping<M, D, P>(string databaseName, string alias, string providerName)
        where M : IMapper
        where D : IDialect
        where P : IParametrizer
        => AddMapping<M, D, P>(databaseName, new[] { alias }, providerName);

    public void AddMapping<M, D, P>(string databaseName, string[] aliases, string providerName)
        where M : IMapper
        where D : IDialect
        where P : IParametrizer
        => AddMapping(new MapperInfo(typeof(M), databaseName, aliases, typeof(D), 9, providerName, typeof(P), string.Empty, BrandAttribute.DefaultMainColor, BrandAttribute.DefaultSecondaryColor));

    public void AddMapping(MapperInfo mapperInfo)
    {
        if (!mapperInfo.MapperType.IsAssignableTo(typeof(IMapper)))
            throw new ArgumentException(nameof(mapperInfo.MapperType));

        if (!mapperInfo.DialectType.IsAssignableTo(typeof(IDialect)))
            throw new ArgumentException(nameof(mapperInfo.DialectType));

        if (!mapperInfo.ParametrizerType.IsAssignableTo(typeof(IParametrizer)))
            throw new ArgumentException(nameof(mapperInfo.ParametrizerType));

        if (MapperData.Contains(mapperInfo))
            throw new ArgumentException($"The mapper information for '{mapperInfo.MapperType.Name}' is already registered");

        MapperData.Add(mapperInfo);
    }

    public void RemoveMapping(string alias)
    {
        var mapperData = MapperData.Single(x => x.Aliases.Contains(alias));
        MapperData.Remove(mapperData);
    }

    public void ReplaceMapper(Type oldMapper, Type newMapper)
    {
        var mapperData = MapperData.Single(x => x.MapperType.Equals(oldMapper));
        mapperData.MapperType = newMapper;
    }

    public void ReplaceDriverLocatorFactory(Type mapper, DriverLocatorFactory factory)
        => throw new NotImplementedException();

    #endregion
}
