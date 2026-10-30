using System.Data.Common;

namespace DubUrl;

/// <summary>
/// Explicit, provider-owned mappings for native connection-string defaults.
/// An absent keyword means unsupported; no inference is made from the database protocol.
/// </summary>
public sealed class ConnectionConfigurationCapability
{
    private readonly string? applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout;

    // Preserve the original constructor for already-compiled provider modules.
    public ConnectionConfigurationCapability(string? applicationName, string? pooling,
        string? minPoolSize, string? maxPoolSize, string? connectionTimeout, string? commandTimeout)
        : this(applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout, null, null, null, null) { }

    public ConnectionConfigurationCapability(string? applicationName = null, string? pooling = null,
        string? minPoolSize = null, string? maxPoolSize = null,
        string? connectionTimeout = null, string? commandTimeout = null,
        string? idleTimeout = null, string? maxLifetime = null, string? timeZone = null, string? tls = null)
    {
        (this.applicationName, this.pooling, this.minPoolSize, this.maxPoolSize, this.connectionTimeout, this.commandTimeout)
            = (applicationName, pooling, minPoolSize, maxPoolSize, connectionTimeout, commandTimeout);
        (IdleTimeout, MaxLifetime, TimeZone, Tls) = (idleTimeout, maxLifetime, timeZone, tls);
    }

    public string? IdleTimeout { get; }
    public string? MaxLifetime { get; }
    public string? TimeZone { get; }
    public string? Tls { get; }

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
            case ConnectionConfigurationOption.ApplicationName:
                var name = (string)request.Value;
                var limit = applicationName == "Application Name" && Tls == "Npgsql" ? 63
                    : Tls == "SqlClient" ? 128 : applicationName == "property:ModuleName" ? 48 : applicationName == "ClientApplicationName" ? 255 : int.MaxValue;
                if (System.Text.Encoding.UTF8.GetByteCount(name) > limit)
                    throw new ArgumentException("Application name exceeds the provider's supported length.");
                if (applicationName == "queryband:ApplicationName")
                {
                    var property = builder.GetType().GetProperty("QueryBand") ?? throw new NotSupportedException("The provider does not expose native query bands.");
                    var band = property.GetValue(builder) ?? throw new NotSupportedException("The provider did not create a query band.");
                    try { band.GetType().GetProperty("ApplicationName")!.SetValue(band, name); }
                    catch (System.Reflection.TargetInvocationException) { throw new ArgumentException("The application name is not a valid native query-band value."); }
                    property.SetValue(builder, band);
                }
                else if (!applicationName!.StartsWith("property:", StringComparison.Ordinal)) builder[applicationName] = name;
                break;
            case ConnectionConfigurationOption.ConnectionTimeout: builder[connectionTimeout!] = request.Value; break;
            case ConnectionConfigurationOption.CommandTimeout: builder[commandTimeout!] = request.Value; break;
            case ConnectionConfigurationOption.TimeZone:
                if (!TimeZone!.StartsWith("sql:", StringComparison.Ordinal)) builder[TimeZone] = request.Value;
                break;
            case ConnectionConfigurationOption.Encryption: ApplyEncryption(builder, (ConnectionEncryption)request.Value); break;
            case ConnectionConfigurationOption.Pooling:
                var options = (ConnectionPoolingOptions)request.Value;
                builder[pooling!] = options.Enabled;
                if (options.MinSize.HasValue) builder[minPoolSize!] = options.MinSize.Value;
                if (options.MaxSize.HasValue) builder[maxPoolSize!] = options.MaxSize.Value;
                if (options.IdleTimeout.HasValue) builder[IdleTimeout!] = OptionalConnectionOptions.ValidateTimeout(options.IdleTimeout.Value);
                if (options.MaxLifetime.HasValue) builder[MaxLifetime!] = OptionalConnectionOptions.ValidateTimeout(options.MaxLifetime.Value);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(request));
        }
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
        if (Tls == "Npgsql" && requests.Any(r => r.Option is ConnectionConfigurationOption.ApplicationName or ConnectionConfigurationOption.TimeZone)
            && builder.TryGetValue("No Reset On Close", out var noReset) && Convert.ToBoolean(noReset))
            throw new ArgumentException("Application name and time-zone defaults require native session reset on close.");
        var poolOptions = requests.LastOrDefault(r => r.Option == ConnectionConfigurationOption.Pooling)?.Value as ConnectionPoolingOptions;
        if (poolOptions?.IdleTimeout == TimeSpan.Zero && IdleTimeout != "Connection Idle Timeout")
            throw new NotSupportedException("This provider requires a positive idle timeout; zero is not an infinite idle timeout.");
        if (requests.Any(r => r.Option == ConnectionConfigurationOption.Pooling) && minPoolSize is not null && maxPoolSize is not null
            && builder.TryGetValue(minPoolSize, out var minimum) && builder.TryGetValue(maxPoolSize, out var maximum)
            && Convert.ToInt64(minimum) > Convert.ToInt64(maximum))
            throw new ArgumentException("Minimum pool size cannot exceed the effective maximum pool size.");
        var zone = requests.LastOrDefault(r => r.Option == ConnectionConfigurationOption.TimeZone)?.Value as string;
        if (zone is null) return;
        if (TimeZone is "sql:oracle" or "sql:firebird" or "sql:teradata")
        {
            if (pooling is null || !builder.TryGetValue(pooling, out var pool) || Convert.ToBoolean(pool))
                throw new NotSupportedException("Session time zones on this route require explicitly disabled pooling to prevent session-state leakage.");
        }
        if (TimeZone == "sql:mysql" && (!builder.TryGetValue("Pooling", out var poolingValue) || Convert.ToBoolean(poolingValue))
            && builder.TryGetValue("Connection Reset", out var reset) && !Convert.ToBoolean(reset))
            throw new ArgumentException("Session time zones require native connection reset on pool checkout.");
        if (TimeZone == "Timezone" && zone[0] is '+' or '-')
            throw new NotSupportedException("Npgsql time zones require a named zone such as Europe/Brussels or Etc/GMT-2; numeric offset strings have different PostgreSQL POSIX semantics.");
        if (TimeZone == "sql:teradata" && zone[0] is not ('+' or '-'))
            throw new NotSupportedException("Teradata shared time zones currently accept signed HH:mm offsets only.");
    }

    private void ApplyEncryption(DbConnectionStringBuilder builder, ConnectionEncryption encryption)
    {
        // Inspect explicit values only: native builder getters can also expose defaults.
        var supplied = new DbConnectionStringBuilder { ConnectionString = builder.ConnectionString };
        if (Tls == "Teradata")
        {
            RequireCompatible(supplied, "SslMode", "Verify-Full", "VerifyFull");
            builder["SslMode"] = "Verify-Full";
            if (encryption.CaPath is not null)
            {
                RequireCompatible(supplied, "SslCA", encryption.CaPath);
                builder["SslCA"] = encryption.CaPath;
            }
            return;
        }
        if (Tls == "Trino")
        {
            if (encryption.CaPath is not null)
                throw new NotSupportedException("NReco Trino requires external system trust; custom CA files are unsupported.");
            RequireCompatible(supplied, "UseSsl", "True");
            builder["UseSsl"] = true;
            return;
        }
        if (Tls is "Odbc" or "OleDb")
        {
            TransportEncryption.Apply(builder, encryption, Tls == "Odbc");
            return;
        }
        if (Tls == "SqlClient")
        {
            if (encryption.CaPath is not null)
                throw new NotSupportedException("SqlClient cannot use a custom CA file. Configure external system trust; ServerCertificate is certificate pinning, not CA trust.");
            RequireCompatible(supplied, "Encrypt", "True", "Mandatory", "Strict");
            RequireCompatible(supplied, "TrustServerCertificate", "False");
            RejectExplicit(supplied, "ServerCertificate", "HostNameInCertificate");
            if (!supplied.Keys.Cast<string>().Any(k => Normalize(k) == "encrypt")) builder["Encrypt"] = "Mandatory";
            builder["Trust Server Certificate"] = false;
            return;
        }
        if (Tls is not ("Npgsql" or "MySql" or "ConnectorNet")) throw new NotSupportedException("Unknown TLS mapping.");
        if (Tls is "MySql" or "ConnectorNet")
        {
            foreach (string key in supplied.Keys)
                if ((Normalize(key) is "connectionprotocol" or "protocol" && !new[] { "socket", "tcp" }.Contains(supplied[key].ToString(), StringComparer.OrdinalIgnoreCase))
                    || (Normalize(key) is "server" or "host" && supplied[key].ToString()!.StartsWith('/')))
                    throw new NotSupportedException("TLS requires a TCP transport.");
        }
        RequireCompatible(supplied, "SSLMode", "VerifyFull");
        RejectExplicit(supplied, "TrustServerCertificate", "TrustServerCertificateCallback");
        if (Tls == "Npgsql")
        {
            if (supplied.TryGetValue("Host", out var host) && host.ToString()!.StartsWith('/'))
                throw new NotSupportedException("TLS cannot be applied to a Unix-domain socket.");
            RequireCompatible(supplied, "GSSEncryptionMode", "Disable");
            builder["GSS Encryption Mode"] = "Disable";
        }
        builder["SSL Mode"] = "VerifyFull";
        if (encryption.CaPath is not null)
        {
            var caKeyword = Tls == "Npgsql" ? "Root Certificate" : Tls == "ConnectorNet" ? "SslCa" : "SSL CA";
            RequireCompatible(supplied, Normalize(caKeyword), encryption.CaPath);
            builder[caKeyword] = encryption.CaPath;
        }
    }

    private static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static void RequireCompatible(DbConnectionStringBuilder supplied, string keyword, params string[] allowed)
    {
        foreach (string key in supplied.Keys)
            if (Normalize(key) == Normalize(keyword) && !(Normalize(keyword) is "rootcertificate" or "sslca"
                ? allowed.Any(path => string.Equals(Path.GetFullPath(path), Path.GetFullPath(Convert.ToString(supplied[key])!),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                : allowed.Contains(Convert.ToString(supplied[key]), StringComparer.OrdinalIgnoreCase)))
                throw new ArgumentException("Explicit TLS configuration conflicts with a native URL option.");
    }
    private static void RejectExplicit(DbConnectionStringBuilder supplied, params string[] keywords)
    {
        foreach (string key in supplied.Keys)
            if (keywords.Any(keyword => Normalize(keyword) == Normalize(key)))
                throw new ArgumentException("Explicit TLS configuration conflicts with a native verification override.");
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
        => new(ApplicationName, Pooling, MinPoolSize, MaxPoolSize, ConnectionTimeout, CommandTimeout, IdleTimeout, MaxLifetime, TimeZone, Tls);
}
