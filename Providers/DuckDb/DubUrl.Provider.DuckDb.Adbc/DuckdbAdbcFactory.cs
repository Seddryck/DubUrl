using DubUrl.Adbc;
using DubUrl.Provider.DuckDb.Adbc.Drivers.Discovery;

namespace DubUrl.Provider.DuckDb.Adbc;

/// <summary>Creates DuckDB ADBC connections and sessions using installed-driver discovery.</summary>
public sealed class DuckdbAdbcFactory : AdbcProviderFactory
{
    /// <summary>Discovery is deferred until each connection or session is opened.</summary>
    public static readonly DuckdbAdbcFactory Instance = new();

    private DuckdbAdbcFactory() : base(() => new DuckdbDriverDiscovery().Load()) { }
}
