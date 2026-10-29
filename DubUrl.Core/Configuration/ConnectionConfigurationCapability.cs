using System.Data.Common;

namespace DubUrl;

/// <summary>
/// Explicit, provider-owned mappings for native connection-string defaults.
/// An absent keyword means unsupported; no inference is made from the database protocol.
/// </summary>
public sealed class ConnectionConfigurationCapability
{
    private readonly string? applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout;

    public ConnectionConfigurationCapability(string? applicationName = null, string? pooling = null,
        string? minPoolSize = null, string? maxPoolSize = null,
        string? connectionTimeout = null, string? commandTimeout = null)
        => (this.applicationName, this.pooling, this.minPoolSize, this.maxPoolSize, this.connectionTimeout, this.commandTimeout)
            = (applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout);

    internal bool Supports(ConnectionConfigurationRequest request)
        => request.Option switch
        {
            ConnectionConfigurationOption.ApplicationName => applicationName is not null,
            ConnectionConfigurationOption.Pooling => pooling is not null
                && (request.Value is ConnectionPoolingOptions options)
                && (!options.MinSize.HasValue || minPoolSize is not null)
                && (!options.MaxSize.HasValue || maxPoolSize is not null),
            ConnectionConfigurationOption.ConnectionTimeout => connectionTimeout is not null,
            ConnectionConfigurationOption.CommandTimeout => commandTimeout is not null,
            _ => false
        };

    internal void Apply(DbConnectionStringBuilder builder, ConnectionConfigurationRequest request)
    {
        switch (request.Option)
        {
            case ConnectionConfigurationOption.ApplicationName: builder[applicationName!] = request.Value; break;
            case ConnectionConfigurationOption.ConnectionTimeout: builder[connectionTimeout!] = request.Value; break;
            case ConnectionConfigurationOption.CommandTimeout: builder[commandTimeout!] = request.Value; break;
            case ConnectionConfigurationOption.Pooling:
                var options = (ConnectionPoolingOptions)request.Value;
                builder[pooling!] = options.Enabled;
                if (options.MinSize.HasValue) builder[minPoolSize!] = options.MinSize.Value;
                if (options.MaxSize.HasValue) builder[maxPoolSize!] = options.MaxSize.Value;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(request));
        }
    }
}

/// <summary>Declares mappings on a specific mapper/driver route. Deliberately not inherited.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ConnectionConfigurationAttribute : Attribute
{
    public string? ApplicationName { get; set; }
    public string? Pooling { get; set; }
    public string? MinPoolSize { get; set; }
    public string? MaxPoolSize { get; set; }
    public string? ConnectionTimeout { get; set; }
    public string? CommandTimeout { get; set; }

    internal ConnectionConfigurationCapability Create()
        => new(ApplicationName, Pooling, MinPoolSize, MaxPoolSize, ConnectionTimeout, CommandTimeout);
}
