using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Parsing;

namespace DubUrl.Adbc;

/// <summary>Opens an Arrow-native ADBC session using DubUrl's URL mappings.</summary>
public sealed class AdbcConnectionUrl
{
    private readonly ISchemeRegistry registry;

    public string Url { get; }

    /// <summary>Uses the default provider mappings and registered ADBC factory.</summary>
    public AdbcConnectionUrl(string url) : this(url, DefaultRegistry.Get()) { }

    public AdbcConnectionUrl(string url, ISchemeRegistry registry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        Url = url;
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Resolves the URL and opens an owning session. The caller must dispose statements,
    /// result streams, and batches before disposing the session.
    /// </summary>
    public AdbcSession Open()
    {
        var resolved = registry.Resolve(ConnectionUri.Parse(Url));
        if (resolved.ProviderFactory is not AdbcProviderFactory factory)
            throw new NotSupportedException("Arrow-native access requires an AdbcProviderFactory. Use an ADBC URL with a matching factory registration. For an installed connector, use matching DubUrl.Adbc and Apache Arrow API versions in the application and connector, then refresh the registry.");

        var builder = new DbConnectionStringBuilder { ConnectionString = resolved.ConnectionString };
        var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string key in builder.Keys)
            parameters.Add(key, Convert.ToString(builder[key], System.Globalization.CultureInfo.InvariantCulture)!);

        return factory.OpenSession(parameters);
    }
}
