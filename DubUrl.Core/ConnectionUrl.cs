using DubUrl.Extensions;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl;

public class ConnectionUrl : BaseConnectionUrl
{
    private ConnectionConfigurationRequest[] configuration = [];
    private bool readOnly;
    private IReadOnlyList<SkippedConnectionOption> skippedOptions = Array.Empty<SkippedConnectionOption>();

    /// <summary>Resolves configuration and reports unsupported optional settings without credentials.</summary>
    public IReadOnlyList<SkippedConnectionOption> SkippedOptions
    {
        get { _ = Result; return skippedOptions; }
    }

    public ConnectionUrl WithApplicationName(string applicationName)
        => WithRequest(ConnectionConfigurationOption.ApplicationName, OptionalConnectionOptions.ValidateApplicationName(applicationName));

    public ConnectionUrl WithPooling(ConnectionPoolingOptions options)
        => WithRequest(ConnectionConfigurationOption.Pooling, OptionalConnectionOptions.ValidatePooling(options));

    public ConnectionUrl WithConnectionTimeout(TimeSpan timeout)
        => WithRequest(ConnectionConfigurationOption.ConnectionTimeout, OptionalConnectionOptions.ValidateTimeout(timeout));

    public ConnectionUrl WithCommandTimeout(TimeSpan timeout)
        => WithRequest(ConnectionConfigurationOption.CommandTimeout, OptionalConnectionOptions.ValidateTimeout(timeout));

    /// <summary>Requires native read-only access to an existing primary database file.</summary>
    /// <remarks>Strict and idempotent. Unsupported routes throw before creating a connection.
    /// Temporary objects, attached databases and external side effects follow native engine rules.</remarks>
    public ConnectionUrl WithReadOnly()
        => new(Url, Parser, Registry) { configuration = configuration, readOnly = true };

    /// <summary>Skips unsupported settings only. Invalid values and provider failures still throw.</summary>
    public ConnectionUrl WithOptional(Action<OptionalConnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var options = new OptionalConnectionOptions();
        configure(options);
        return WithRequests(options.Snapshot());
    }

    private ConnectionUrl WithRequest(ConnectionConfigurationOption option, object value)
        => WithRequests([new(option, value, false)]);

    private ConnectionUrl WithRequests(ConnectionConfigurationRequest[] requests)
        => new(Url, Parser, Registry) { configuration = [.. configuration, .. requests], readOnly = readOnly };

    protected override ResolvedConnection ResolveConnection(ConnectionUri connectionUri)
    {
        var resolved = base.ResolveConnection(connectionUri);
        if (configuration.Length == 0 && !readOnly) return resolved;
        Registry.TryGetCapability<ConnectionConfigurationCapability>(connectionUri.Scheme, out var capability);
        var skipped = new List<SkippedConnectionOption>();
        var applicable = new List<ConnectionConfigurationRequest>();
        var provider = resolved.ProviderFactory.GetType().FullName ?? resolved.ProviderFactory.GetType().Name;
        ReadOnlyConnectionCapability? readOnlyCapability = null;
        if (readOnly && (!Registry.TryGetCapability(connectionUri.Scheme, out readOnlyCapability) || readOnlyCapability is null))
            throw new NotSupportedException($"ReadOnly is not supported for database route '{connectionUri.Scheme.GetRegistryAlias()}' using provider '{provider}'. A transaction default or replica-routing hint is not native read-only file access.");
        foreach (var group in configuration.GroupBy(request => request.Option))
        {
            // An optional call must never weaken a required setting, irrespective of call order.
            var request = group.LastOrDefault(item => !item.Optional) ?? group.Last();
            if (capability?.Supports(request) == true) applicable.Add(request);
            else if (request.Optional) skipped.Add(new(request.Option, provider, "Unsupported by the selected provider route or requested pool-size configuration."));
            else throw new NotSupportedException($"{request.Option} is not supported for database route '{connectionUri.Scheme.GetRegistryAlias()}' using provider '{provider}'.");
        }
        if (applicable.Count == 0 && !readOnly)
        {
            skippedOptions = skipped.AsReadOnly();
            return resolved;
        }
        var builder = resolved.ProviderFactory.CreateConnectionStringBuilder()
            ?? throw new NotSupportedException($"Provider '{provider}' does not supply a connection-string builder for shared configuration.");
        builder.ConnectionString = resolved.ConnectionString;
        foreach (var request in applicable) capability!.Apply(builder, request);
        // Always last: optional/native options cannot silently weaken the required access mode.
        if (readOnly) readOnlyCapability!.Apply(builder);
        skippedOptions = skipped.AsReadOnly();
        return resolved with { ConnectionString = builder.ConnectionString };
    }

    public ConnectionUrl(string url)
        : this(url, DefaultRegistry.Get()) { }

    public ConnectionUrl(string url, ISchemeRegistry registry)
        : this(url, new Parser(), registry) { }

    internal ConnectionUrl(string url, IParser parser, ISchemeRegistry registry)
        : base(url, parser, registry) { }

    public virtual IDbConnection Connect()
    {
        var connection = GetProviderFactory().CreateConnection() ?? throw new ArgumentNullException();
        try
        {
            connection.ConnectionString = Result.ConnectionString;
            if (readOnly && TryGetCapability<ReadOnlyConnectionCapability>(out var capability))
                capability!.Configure(connection);
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    public virtual IDbConnection Open()
    {
        var connection = Connect();
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }
}
