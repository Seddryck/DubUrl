using System.Data;
using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Parsing;

namespace DubUrl.ProviderTesting;

/// <summary>Records ownership while forwarding connection strings, I/O and commands to the real native driver.</summary>
internal sealed class PasswordQaRegistry(ISchemeRegistry inner) : ISchemeRegistry
{
    internal List<PasswordQaConnection> Connections { get; } = [];
    public ResolvedConnection Resolve(ConnectionUri uri) => Observe(inner.Resolve(uri));
    private ResolvedConnection Observe(ResolvedConnection resolved)
        => resolved with { ProviderFactory = new ObservedFactory(resolved.ProviderFactory, Connections) };
    public bool TryResolve(ConnectionUri uri, out ResolvedConnection? resolved, out ConnectionResolutionDiagnostic? diagnostic)
    {
        if (!inner.TryResolve(uri, out resolved, out diagnostic)) return false;
        resolved = Observe(resolved!);
        return true;
    }
    public IMapper GetMapper(string alias) => inner.GetMapper(alias);
    public IMapper GetMapper(SchemeKey scheme) => inner.GetMapper(scheme);
    public DbProviderFactory GetProviderFactory(SchemeKey scheme) => new ObservedFactory(inner.GetProviderFactory(scheme), Connections);
    public bool TryGetCapability<T>(SchemeKey scheme, out T? capability) where T : class => inner.TryGetCapability(scheme, out capability);
    public bool CanHandle(string scheme) => inner.CanHandle(scheme);

    private sealed class ObservedFactory(DbProviderFactory factory, List<PasswordQaConnection> connections) : DbProviderFactory
    {
        public override DbConnection CreateConnection()
        {
            var observed = new PasswordQaConnection(factory.CreateConnection() ?? throw new InvalidOperationException("The QA driver created no connection."));
            connections.Add(observed);
            return observed;
        }
    }
}

internal sealed class PasswordQaConnection(DbConnection native) : DbConnection
{
    internal DbConnection Native { get; } = native;
    internal bool WasDisposed { get; private set; }
    internal bool OpenStarted { get; private set; }
    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string ConnectionString { get => Native.ConnectionString; set => Native.ConnectionString = value; }
    public override string Database => Native.Database;
    public override string DataSource => Native.DataSource;
    public override string ServerVersion => Native.ServerVersion;
    public override ConnectionState State => Native.State;
    public override void ChangeDatabase(string databaseName) => Native.ChangeDatabase(databaseName);
    public override void Close() => Native.Close();
    public override Task CloseAsync() => Native.CloseAsync();
    public override void Open() { OpenStarted = true; Native.Open(); }
    public override Task OpenAsync(CancellationToken cancellationToken) { OpenStarted = true; return Native.OpenAsync(cancellationToken); }
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => Native.BeginTransaction(isolationLevel);
    protected override DbCommand CreateDbCommand() => Native.CreateCommand();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !WasDisposed) { Native.Dispose(); WasDisposed = true; }
        base.Dispose(disposing);
    }
    public override async ValueTask DisposeAsync()
    {
        if (WasDisposed) return;
        await Native.DisposeAsync();
        WasDisposed = true;
        GC.SuppressFinalize(this);
    }
}
