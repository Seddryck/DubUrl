using DubUrl.Rewriting;
using DubUrl.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace DubUrl.Mapping;

public class SchemeRegistry : ISchemeRegistry
{
    private readonly Dictionary<string, SchemeRegistration> _registrations;

    public SchemeRegistry(Dictionary<string, Func<IMapper>> mapperFactories)
        : this(ProviderCatalogBuilder.AdaptMapperFactories(mapperFactories)) { }

    private SchemeRegistry(Dictionary<string, SchemeRegistration> registrations)
    {
        _registrations = new(registrations, StringComparer.OrdinalIgnoreCase);
    }

    internal static SchemeRegistry FromRegistrations(Dictionary<string, SchemeRegistration> registrations)
        => new(registrations);

    public ResolvedConnection Resolve(ConnectionUri connectionUri)
    {
        var registration = GetRegistration(connectionUri.Scheme);
        var mapper = registration.MapperFactory();
        mapper.Rewrite(connectionUri);

        return new ResolvedConnection(
            mapper.GetConnectionString(),
            mapper.GetDialect(),
            mapper.GetConnectivity(),
            mapper.GetParametrizer(),
            registration.ProviderFactory(mapper)
        )
        {
            BulkCopyProvider = registration.BulkCopyProvider
        };
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
        => GetRegistration(scheme).MapperFactory();

    private SchemeRegistration GetRegistration(SchemeKey scheme)
    {
        var alias = scheme.GetRegistryAlias();

        if (!_registrations.TryGetValue(alias, out var registration))
            throw new SchemeNotFoundException(alias, [.. _registrations.Keys]);

        return registration;
    }

    public DbProviderFactory GetProviderFactory(SchemeKey scheme)
        => GetRegistration(scheme).ProviderFactory(null);

    public DbProviderFactory GetProviderFactory(string[] schemes)
        => GetProviderFactory(new SchemeKey(schemes));

    public bool TryGetCapability<TCapability>(SchemeKey scheme, out TCapability? capability)
        where TCapability : class
    {
        var alias = scheme.GetRegistryAlias();
        if (typeof(TCapability) == typeof(DubUrl.BulkCopy.IBulkCopyProvider)
            && _registrations.TryGetValue(alias, out var bulkRegistration)
            && bulkRegistration.BulkCopyProvider is TCapability bulkCopy)
        {
            capability = bulkCopy;
            return true;
        }
        if (_registrations.TryGetValue(alias, out var registration)
            && registration.Capabilities.TryGetValue(typeof(TCapability), out var value))
        {
            capability = (TCapability)value;
            return true;
        }

        capability = null;
        return false;
    }

    public bool CanHandle(string scheme)
        => _registrations.ContainsKey(SchemeKey.Parse(scheme).GetRegistryAlias());
}
