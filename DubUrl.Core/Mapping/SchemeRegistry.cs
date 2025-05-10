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
    private readonly Dictionary<string, IMapper> _mappers;

    public SchemeRegistry(Dictionary<string, IMapper> mappers)
        => _mappers = new(mappers); // Defensive copy

    public IMapper GetMapper(string scheme)
        => GetMapper([scheme]);

    public IMapper GetMapper(string[] schemes)
    {
        var alias = SchemeRegistryBuilder.GetAlias(schemes);

        if (!_mappers.TryGetValue(alias, out var mapper))
            throw new SchemeNotFoundException(alias, [.. _mappers.Keys]);

        return mapper;
    }

    public DbProviderFactory GetProviderFactory(string[] schemes)
    {
        var mapper = GetMapper(schemes);
        return SchemeRegistryBuilder.GetProvider(mapper.GetProviderName())
            ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
    }

    public bool CanHandle(string scheme)
        => _mappers.ContainsKey(SchemeRegistryBuilder.GetAlias(scheme.Split(['+', ':'])));
}
