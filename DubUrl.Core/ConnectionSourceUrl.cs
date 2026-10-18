using DubUrl.Mapping;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl;

/// <summary>Resolves a URL into a long-lived, explicitly owned connection source.</summary>
public sealed class ConnectionSourceUrl : BaseConnectionUrl
{
    public ConnectionSourceUrl(string url) : this(url, SchemeRegistryBuilder.GetDefault()) { }
    public ConnectionSourceUrl(string url, ISchemeRegistry registry) : base(url, new Parser(), registry) { }

    public ConnectionSource Create(ConnectionSourceOptions? options = null)
    {
        var mode = (options ?? new()).ReuseMode;
        if (mode != ConnectionReuseMode.ProviderManaged && mode != ConnectionReuseMode.RetainedExclusive)
            throw new ArgumentOutOfRangeException(nameof(options));
        // Bind both the connection and query metadata to this exact resolution.
        var context = new ConnectionUrl(Url, new ResolvedRegistry(Result, Registry));
        DbDataSource? dataSource;
        try
        {
            dataSource = Result.ProviderFactory.CreateDataSource(Result.ConnectionString);
        }
        catch (NotSupportedException) { dataSource = null; }
        catch (NotImplementedException) { dataSource = null; }
        return new ConnectionSource(context, mode, dataSource);
    }

    private sealed class ResolvedRegistry(ResolvedConnection resolved, ISchemeRegistry registry) : ISchemeRegistry
    {
        public ResolvedConnection Resolve(ConnectionUri uri) => resolved;
        public DbProviderFactory GetProviderFactory(SchemeKey scheme) => resolved.ProviderFactory;
        public bool TryResolve(ConnectionUri uri, out ResolvedConnection? connection, out ConnectionResolutionDiagnostic? diagnostic)
        {
            connection = resolved;
            diagnostic = null;
            return true;
        }
        public IMapper GetMapper(string alias) => registry.GetMapper(alias);
        public IMapper GetMapper(SchemeKey scheme) => registry.GetMapper(scheme);
        public bool CanHandle(string scheme) => registry.CanHandle(scheme);
    }
}
