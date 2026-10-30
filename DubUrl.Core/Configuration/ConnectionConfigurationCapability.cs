using System.Data.Common;

namespace DubUrl;

/// <summary>
/// Explicit, provider-owned mappings for native connection-string defaults.
/// An absent keyword means unsupported; no inference is made from the database protocol.
/// </summary>
public sealed class ConnectionConfigurationCapability
{
    private readonly string? applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout;

    private const string Npgsql = "Npgsql";

    public ConnectionConfigurationCapability(string? applicationName = null, string? pooling = null,
        string? minPoolSize = null, string? maxPoolSize = null,
        string? connectionTimeout = null, string? commandTimeout = null)
        => (this.applicationName, this.pooling, this.minPoolSize, this.maxPoolSize, this.connectionTimeout, this.commandTimeout)
            = (applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout);

    public string? IdleTimeout { get; init; }
    public string? MaxLifetime { get; init; }
    public string? TimeZone { get; init; }
    public string? Tls { get; init; }

    internal bool Supports(ConnectionConfigurationRequest request)
        => request.Option switch
        {
            ConnectionConfigurationOption.ApplicationName => applicationName is not null,
            ConnectionConfigurationOption.Pooling => pooling is not null
                && (request.Value is ConnectionPoolingOptions options)
                && (!options.MinSize.HasValue || minPoolSize is not null)
                && (!options.MaxSize.HasValue || maxPoolSize is not null)
                && (!options.IdleTimeout.HasValue || IdleTimeout is not null)
                && (!options.MaxLifetime.HasValue || MaxLifetime is not null),
            ConnectionConfigurationOption.ConnectionTimeout => connectionTimeout is not null,
            ConnectionConfigurationOption.CommandTimeout => commandTimeout is not null,
            ConnectionConfigurationOption.TimeZone => TimeZone is not null,
            ConnectionConfigurationOption.Encryption => Tls is not null,
            _ => false
        };

    internal void Apply(DbConnectionStringBuilder builder, ConnectionConfigurationRequest request)
    {
        switch (request.Option)
        {
            case ConnectionConfigurationOption.ApplicationName: ApplyApplicationName(builder, (string)request.Value); break;
            case ConnectionConfigurationOption.ConnectionTimeout: builder[RequiredKeyword(connectionTimeout)] = request.Value; break;
            case ConnectionConfigurationOption.CommandTimeout: builder[RequiredKeyword(commandTimeout)] = request.Value; break;
            case ConnectionConfigurationOption.TimeZone:
                var mapping = RequiredKeyword(TimeZone);
                if (!mapping.StartsWith("sql:", StringComparison.Ordinal)) builder[mapping] = request.Value;
                break;
            case ConnectionConfigurationOption.Encryption: NativeEncryption.Apply(Tls, builder, (ConnectionEncryption)request.Value); break;
            case ConnectionConfigurationOption.Pooling: ApplyPooling(builder, (ConnectionPoolingOptions)request.Value); break;
            default: throw new ArgumentOutOfRangeException(nameof(request));
        }
    }

    private static string RequiredKeyword(string? keyword)
        => keyword ?? throw new NotSupportedException("The provider does not define this native connection option.");

    private int ApplicationNameLimit => (applicationName, Tls) switch
    {
        ("Application Name", Npgsql) => 63,
        (_, "SqlClient") => 128,
        ("property:ModuleName", _) => 48,
        ("ClientApplicationName", _) => 255,
        _ => int.MaxValue
    };

    private void ApplyApplicationName(DbConnectionStringBuilder builder, string name)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(name) > ApplicationNameLimit)
            throw new ArgumentException("Application name exceeds the provider's supported length.");
        var keyword = RequiredKeyword(applicationName);
        if (keyword == "queryband:ApplicationName") ApplyQueryBand(builder, name);
        else if (!keyword.StartsWith("property:", StringComparison.Ordinal)) builder[keyword] = name;
    }

    private static void ApplyQueryBand(DbConnectionStringBuilder builder, string name)
    {
        var property = builder.GetType().GetProperty("QueryBand") ?? throw new NotSupportedException("The provider does not expose native query bands.");
        var band = property.GetValue(builder) ?? throw new NotSupportedException("The provider did not create a query band.");
        var application = band.GetType().GetProperty("ApplicationName") ?? throw new NotSupportedException("The provider does not expose query-band application names.");
        try { application.SetValue(band, name); }
        catch (System.Reflection.TargetInvocationException) { throw new ArgumentException("The application name is not a valid native query-band value."); }
        property.SetValue(builder, band);
    }

    private void ApplyPooling(DbConnectionStringBuilder builder, ConnectionPoolingOptions options)
    {
        builder[RequiredKeyword(pooling)] = options.Enabled;
        if (options.MinSize.HasValue) builder[RequiredKeyword(minPoolSize)] = options.MinSize.Value;
        if (options.MaxSize.HasValue) builder[RequiredKeyword(maxPoolSize)] = options.MaxSize.Value;
        if (options.IdleTimeout.HasValue) builder[RequiredKeyword(IdleTimeout)] = OptionalConnectionOptions.ValidateTimeout(options.IdleTimeout.Value);
        if (options.MaxLifetime.HasValue) builder[RequiredKeyword(MaxLifetime)] = OptionalConnectionOptions.ValidateTimeout(options.MaxLifetime.Value);
    }

    internal void Configure(DbConnection connection, IEnumerable<ConnectionConfigurationRequest> requests)
    {
        var application = requests.Where(r => r.Option == ConnectionConfigurationOption.ApplicationName).ToArray();
        var name = (application.LastOrDefault(r => !r.Optional) ?? application.LastOrDefault())?.Value as string;
        if (name is not null && applicationName?.StartsWith("property:", StringComparison.Ordinal) == true)
        {
            var property = connection.GetType().GetProperty(applicationName[9..])
                ?? throw new NotSupportedException("The installed provider does not expose the required application metadata property.");
            connection.StateChange += (_, change) =>
            {
                if (change.CurrentState != System.Data.ConnectionState.Open) return;
                try { property.SetValue(connection, name); }
                catch { connection.Dispose(); throw; }
            };
        }
        var zone = requests.LastOrDefault(r => r.Option == ConnectionConfigurationOption.TimeZone);
        if (zone is not null && TimeZone?.StartsWith("sql:", StringComparison.Ordinal) == true)
            SessionTimeZone.Configure(connection, TimeZone, (string)zone.Value);
    }

    internal void ValidateNative(DbConnectionStringBuilder builder, IReadOnlyList<ConnectionConfigurationRequest> requests)
    {
        ValidateSessionReset(builder, requests);
        var pool = requests.LastOrDefault(r => r.Option == ConnectionConfigurationOption.Pooling)?.Value as ConnectionPoolingOptions;
        if (pool is not null) ValidatePool(builder, pool);
        var zone = requests.LastOrDefault(r => r.Option == ConnectionConfigurationOption.TimeZone)?.Value as string;
        if (zone is not null) ValidateTimeZone(builder, zone);
    }

    private void ValidateSessionReset(DbConnectionStringBuilder builder, IReadOnlyList<ConnectionConfigurationRequest> requests)
    {
        if (Tls != Npgsql) return;
        var sessionDefault = requests.Any(r => r.Option is ConnectionConfigurationOption.ApplicationName or ConnectionConfigurationOption.TimeZone);
        if (sessionDefault && builder.TryGetValue("No Reset On Close", out var noReset) && Convert.ToBoolean(noReset))
            throw new ArgumentException("Application name and time-zone defaults require native session reset on close.");
    }

    private void ValidatePool(DbConnectionStringBuilder builder, ConnectionPoolingOptions options)
    {
        if (options.IdleTimeout == TimeSpan.Zero && IdleTimeout != "Connection Idle Timeout")
            throw new NotSupportedException("This provider requires a positive idle timeout; zero is not an infinite idle timeout.");
        if (minPoolSize is null || maxPoolSize is null) return;
        if (builder.TryGetValue(minPoolSize, out var minimum) && builder.TryGetValue(maxPoolSize, out var maximum)
            && Convert.ToInt64(minimum) > Convert.ToInt64(maximum))
            throw new ArgumentException("Minimum pool size cannot exceed the effective maximum pool size.");
    }

    private void ValidateTimeZone(DbConnectionStringBuilder builder, string zone)
    {
        if (TimeZone is "sql:oracle" or "sql:firebird" or "sql:teradata" && IsPoolingEnabled(builder, pooling))
            throw new NotSupportedException("Session time zones on this route require explicitly disabled pooling to prevent session-state leakage.");
        if (TimeZone == "sql:mysql" && IsPoolingEnabled(builder, "Pooling")) ValidateMySqlReset(builder);
        if (TimeZone == "Timezone" && zone[0] is '+' or '-')
            throw new NotSupportedException("Npgsql time zones require a named zone such as Europe/Brussels or Etc/GMT-2; numeric offset strings have different PostgreSQL POSIX semantics.");
        if (TimeZone == "sql:teradata" && zone[0] is not ('+' or '-'))
            throw new NotSupportedException("Teradata shared time zones currently accept signed HH:mm offsets only.");
    }

    private static bool IsPoolingEnabled(DbConnectionStringBuilder builder, string? keyword)
        => keyword is null || !builder.TryGetValue(keyword, out var value) || Convert.ToBoolean(value);

    private static void ValidateMySqlReset(DbConnectionStringBuilder builder)
    {
        if (builder.TryGetValue("Connection Reset", out var reset) && !Convert.ToBoolean(reset))
            throw new ArgumentException("Session time zones require native connection reset on pool checkout.");
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
    public string? IdleTimeout { get; set; }
    public string? MaxLifetime { get; set; }
    public string? TimeZone { get; set; }
    public string? Tls { get; set; }

    internal ConnectionConfigurationCapability Create()
        => new(ApplicationName, Pooling, MinPoolSize, MaxPoolSize, ConnectionTimeout, CommandTimeout)
        { IdleTimeout = IdleTimeout, MaxLifetime = MaxLifetime, TimeZone = TimeZone, Tls = Tls };
}
