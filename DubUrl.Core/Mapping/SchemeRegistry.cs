using DubUrl.Locating.OdbcDriver;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting;
using DubUrl.Parsing;
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

    public ResolvedConnection Resolve(ConnectionUri connectionUri)
    {
        var mapper = GetMapper(connectionUri.Scheme);
        mapper.Rewrite(connectionUri);

        return new ResolvedConnection(
            mapper.GetConnectionString(),
            mapper.GetDialect(),
            mapper.GetConnectivity(),
            mapper.GetParametrizer(),
            GetProviderFactory(mapper)
        );
    }

    public bool TryResolve(
        ConnectionUri connectionUri,
        out ResolvedConnection? resolvedConnection,
        out ConnectionResolutionDiagnostic? diagnostic)
    {
        try
        {
            resolvedConnection = Resolve(connectionUri);
            diagnostic = null;
            return true;
        }
        catch (DubUrlException exception)
        {
            resolvedConnection = null;
            diagnostic = new(
                exception switch
                {
                    SchemeNotFoundException => ConnectionResolutionDiagnosticCode.SchemeNotFound,
                    ProviderNotFoundException => ConnectionResolutionDiagnosticCode.ProviderNotFound,
                    InvalidConnectionUrlException => ConnectionResolutionDiagnosticCode.InvalidConnectionUri,
                    _ => ConnectionResolutionDiagnosticCode.ResolutionFailed,
                },
                Redact(exception.Message, connectionUri.Password));
            return false;
        }
    }

    private static string Redact(string message, string password)
        => string.IsNullOrEmpty(password)
            ? message
            : message.Replace(password, "***", StringComparison.Ordinal);

    public IMapper GetMapper(string scheme)
        => GetMapper(SchemeKey.Parse(scheme));

    public IMapper GetMapper(string[] schemes)
        => GetMapper(new SchemeKey(schemes));

    public IMapper GetMapper(SchemeKey scheme)
    {
        var alias = SchemeRegistryBuilder.GetAlias(scheme);

        if (!_mapperFactories.TryGetValue(alias, out var mapperFactory))
            throw new SchemeNotFoundException(alias, [.. _mapperFactories.Keys]);

        return mapperFactory();
    }

    public DbProviderFactory GetProviderFactory(SchemeKey scheme)
    {
        return GetProviderFactory(GetMapper(scheme));
    }

    public DbProviderFactory GetProviderFactory(string[] schemes)
        => GetProviderFactory(new SchemeKey(schemes));

    private static DbProviderFactory GetProviderFactory(IMapper mapper)
    {
        return SchemeRegistryBuilder.GetProvider(mapper.GetProviderName())
            ?? throw new ProviderNotFoundException(mapper.GetProviderName(), DbProviderFactories.GetProviderInvariantNames().ToArray());
    }

    public bool CanHandle(string scheme)
        => _mapperFactories.ContainsKey(SchemeRegistryBuilder.GetAlias(SchemeKey.Parse(scheme)));
}
