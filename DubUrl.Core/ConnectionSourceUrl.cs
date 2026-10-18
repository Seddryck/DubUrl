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
        var (mode, reason) = ConnectionReusePolicy.Select(options ?? new(),
            new ConnectionReuseContext(Result.ProviderFactory, Result.ConnectionString));
        // Bind both the connection and query metadata to this exact resolution.
        var context = new ConnectionUrl(Url, new ResolvedRegistry(Result, Registry));
        DbDataSource? dataSource = null;
        var factory = Result.ProviderFactory;
        // The inherited implementation is only a framework shim. Use our guarded factory
        // path instead, so failure to set ConnectionString cannot lose the new connection.
        var dataSourceMethod = factory.GetType().GetMethod(nameof(DbProviderFactory.CreateDataSource), [typeof(string)]);
        if (dataSourceMethod is not null && dataSourceMethod.DeclaringType != typeof(DbProviderFactory))
        {
            try
            {
                dataSource = factory.CreateDataSource(Result.ConnectionString);
            }
            catch (Exception exception) when (exception is NotSupportedException or NotImplementedException)
            {
                return new ConnectionSource(context, mode, selectionReason: reason);
            }
        }
        return new ConnectionSource(context, mode, dataSource, reason);
    }

    private sealed class ResolvedRegistry(ResolvedConnection resolved, ISchemeRegistry registry) : ISchemeRegistry
    {
        public ResolvedConnection Resolve(ConnectionUri connectionUri) => resolved;
        public DbProviderFactory GetProviderFactory(SchemeKey scheme) => resolved.ProviderFactory;
        public bool TryResolve(ConnectionUri connectionUri, out ResolvedConnection? resolvedConnection, out ConnectionResolutionDiagnostic? diagnostic)
        {
            resolvedConnection = resolved;
            diagnostic = null;
            return true;
        }
        public IMapper GetMapper(string alias) => registry.GetMapper(alias);
        public IMapper GetMapper(SchemeKey scheme) => registry.GetMapper(scheme);
        public bool CanHandle(string scheme) => registry.CanHandle(scheme);
    }
}
