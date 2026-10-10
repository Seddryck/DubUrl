using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public class SchemeRegistry : ISchemeRegistry
{
    private readonly Dictionary<string, Func<IMapper>> _mapperFactories;

    public SchemeRegistry(Dictionary<string, Func<IMapper>> mapperFactories)
        => _mapperFactories = new(mapperFactories, StringComparer.OrdinalIgnoreCase); // Defensive copy

    public ResolvedConnection Resolve(Parsing.UrlInfo urlInfo)
    {
        var mapper = GetMapper(urlInfo.Schemes);
        mapper.Rewrite(urlInfo);

        return new ResolvedConnection(
            mapper.GetConnectionString(),
            mapper.GetDialect(),
            mapper.GetConnectivity(),
            mapper.GetParametrizer(),
            GetProviderFactory(mapper)
        );
    }

    public IMapper GetMapper(string scheme)
        => GetMapper([scheme]);

    public IMapper GetMapper(string[] schemes)
    {
        var alias = SchemeRegistryBuilder.GetAlias(schemes);

        if (!_mapperFactories.TryGetValue(alias, out var mapperFactory))
            throw new SchemeNotFoundException(alias, [.. _mapperFactories.Keys]);

        return mapperFactory();
    }

    public DbProviderFactory GetProviderFactory(string[] schemes)
    {
        return GetProviderFactory(GetMapper(schemes));
    }

    private static DbProviderFactory GetProviderFactory(IMapper mapper)
    {
        return SchemeRegistryBuilder.GetProvider(mapper.GetProviderName())
            ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
    }

    public bool CanHandle(string scheme)
        => _mapperFactories.ContainsKey(SchemeRegistryBuilder.GetAlias(scheme.Split(['+', ':'])));
}
