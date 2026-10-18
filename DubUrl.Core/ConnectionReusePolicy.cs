using System.Data.Common;

namespace DubUrl;

public interface IConnectionReusePolicyResolver
{
    /// <summary>Returns a concrete mode, or null when this policy does not recognize the configuration.</summary>
    ConnectionReuseMode? Resolve(ConnectionReuseContext context);
}

/// <summary>Provider-aware configuration for reuse policies. Never log the returned builder or its values.</summary>
public sealed class ConnectionReuseContext
{
    private readonly string _connectionString;
    public DbProviderFactory ProviderFactory { get; }
    public string ProviderName => ProviderFactory.GetType().FullName ?? ProviderFactory.GetType().Name;

    internal ConnectionReuseContext(DbProviderFactory factory, string connectionString)
        => (ProviderFactory, _connectionString) = (factory, connectionString);

    public DbConnectionStringBuilder CreateConnectionStringBuilder()
    {
        try
        {
            var builder = ProviderFactory.CreateConnectionStringBuilder()
                ?? throw new NotSupportedException("The provider has no connection string builder.");
            builder.ConnectionString = _connectionString;
            return builder;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            // Provider errors may interpolate credential-bearing keys or values.
            throw new ArgumentException("The provider connection configuration is invalid.");
        }
    }

    public override string ToString() => $"ConnectionReuseContext {{ Provider = {ProviderName}, Configuration = [REDACTED] }}";
}

internal static class ConnectionReusePolicy
{
    public static (ConnectionReuseMode Mode, string Reason) Select(ConnectionSourceOptions options, ConnectionReuseContext context)
    {
        if (options.ReuseMode != ConnectionReuseMode.Auto)
        {
            ValidateConcrete(options.ReuseMode);
            return (options.ReuseMode, "explicit");
        }

        foreach (var resolver in options.PolicyResolvers)
        {
            var decision = resolver.Resolve(context);
            if (decision is null) continue;
            ValidateConcrete(decision.Value);
            return (decision.Value, "registered-policy");
        }

        switch (context.ProviderName)
        {
            case "Npgsql.NpgsqlFactory":
            case "Microsoft.Data.SqlClient.SqlClientFactory":
            case "Microsoft.Data.Sqlite.SqliteFactory":
            {
                // Provider builders apply their own aliases, parsing and defaults.
                var builder = context.CreateConnectionStringBuilder();
                bool pooled;
                try { pooled = Convert.ToBoolean(builder["Pooling"], System.Globalization.CultureInfo.InvariantCulture); }
                catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidCastException)
                { throw new ArgumentException("The provider pooling configuration is invalid."); }
                return (ConnectionReuseMode.ProviderManaged, pooled ? "provider-pooling" : "pooling-disabled-no-retention-policy");
            }
            case "DuckDB.NET.Data.DuckDBClientFactory":
            {
                var builder = context.CreateConnectionStringBuilder();
                var path = Convert.ToString(builder["DataSource"], System.Globalization.CultureInfo.InvariantCulture) ?? "";
                // Local DuckDB benefits from retaining its database instance and connection caches.
                // MotherDuck has different remote-session semantics and remains unclassified.
                if (!path.StartsWith("md:", StringComparison.OrdinalIgnoreCase))
                    return (ConnectionReuseMode.RetainedExclusive, "duckdb-local-retained");
                break;
            }
        }
        return (ConnectionReuseMode.ProviderManaged, "unknown-configuration");
    }

    private static void ValidateConcrete(ConnectionReuseMode mode)
    {
        if (mode != ConnectionReuseMode.ProviderManaged && mode != ConnectionReuseMode.RetainedExclusive)
            throw new ArgumentOutOfRangeException(nameof(mode), "A reuse policy must select a concrete connection strategy.");
    }
}
