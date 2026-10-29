using System.Data;
using System.Data.Common;
using DubUrl.Authentication;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Snowflake.Data.Client;

namespace DubUrl.Cli.Testing;

internal sealed class OAuthCliHarness : IAsyncDisposable
{
    internal const string Schema = "{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"qa_oauth\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\"}]}]}";
    internal static readonly string[] EntraOptions = ["--oauth-client-id", "11111111-1111-1111-1111-111111111111", "--oauth-tenant", "22222222-2222-2222-2222-222222222222"];
    internal ConsoleCapture Console { get; }
    internal Sessions Factory { get; } = new();
    internal Registry Observed { get; }
    internal ServiceProvider Inner { get; }
    internal IServiceProvider Services { get; }
    internal OAuthCliHarness(string url = "mssql://localhost/app", bool interactive = true, Dictionary<string, string?>? configuration = null)
    {
        Console = new(interactive);
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Snowflake.Data", SnowflakeDbFactory.Instance);
        configuration ??= [];
        configuration["ConnectionStrings:test"] = url;
        Inner = CliComposition.CreateDefault(Console, new ConfigurationBuilder().AddInMemoryCollection(configuration).Build(),
            providerModules: [new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, typeof(Provider.Snowflake.Sql.SnowflakeDialect).Assembly])],
            oauthSessionFactory: Factory);
        Observed = new(Inner.GetRequiredService<ISchemeRegistry>());
        Services = new ReplacedRegistry(Inner, Observed);
    }
    internal Task<int> Invoke(string[] arguments, CancellationToken token = default) => CliHost.InvokeAsync(arguments, Services, token);
    public ValueTask DisposeAsync() => Inner.DisposeAsync();
    private sealed class ReplacedRegistry(IServiceProvider inner, Registry registry) : IServiceProvider
    { public object? GetService(Type type) => type == typeof(ISchemeRegistry) ? registry : inner.GetService(type); }

    internal sealed class ConsoleCapture(bool interactive) : ICliConsole
    {
        internal StringWriter Output { get; } = new();
        internal StringWriter Errors { get; } = new();
        public TextReader In => new StringReader(Schema);
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => !interactive;
        public bool IsInteractive => interactive;
    }
    internal sealed class Sessions : ICliOAuthSessionFactory
    {
        internal List<CliOAuthRequest> Requests { get; } = [];
        internal List<Session> Created { get; } = [];
        internal string? Behavior { get; set; }
        internal bool DeviceInstructions { get; set; }
        internal TaskCompletionSource OpenEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IInteractiveOAuthSession Create(DbConnection connection, CliOAuthRequest request, ICliConsole console)
        {
            Requests.Add(request);
            var session = new Session(this, console);
            Created.Add(session);
            return session;
        }
    }
    internal sealed class Session(Sessions owner, ICliConsole console) : IInteractiveOAuthSession, IDisposable
    {
        internal bool Disposed { get; private set; }
        internal int Opens { get; private set; }
        public ValueTask ConfigureAsync(DbConnection connection, AuthenticationContext context, InteractiveOAuthOptions options, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (owner.Behavior == "configuration") throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.AuthenticationFailed);
            return ValueTask.CompletedTask;
        }
        public async ValueTask OpenAsync(DbConnection connection, CancellationToken cancellationToken = default)
        {
            Opens++;
            owner.OpenEntered.TrySetResult();
            if (owner.DeviceInstructions)
                await NativeCliOAuthSessionFactory.PresentDeviceCodeAsync(console, "https://microsoft.com/devicelogin", "ABCD-1234", DateTimeOffset.UtcNow.AddMinutes(5), cancellationToken);
            if (owner.Behavior == "cancel") throw new OperationCanceledException(cancellationToken);
            if (owner.Behavior == "timeout") await Task.Delay(Timeout.Infinite, cancellationToken);
            if (owner.Behavior == "slow") await Task.Delay(1500, cancellationToken);
            if (owner.Behavior == "denied") throw new InvalidOperationException("private-access-token authorization-code verifier-sentinel");
            await connection.OpenAsync(cancellationToken);
        }
        public void Dispose() => Disposed = true;
    }
    internal sealed class Registry(ISchemeRegistry inner) : ISchemeRegistry
    {
        internal List<Connection> Connections { get; } = [];
        internal Reader Metadata { get; } = new();
        public IMapper GetMapper(string alias) => inner.GetMapper(alias);
        public IMapper GetMapper(SchemeKey scheme) => inner.GetMapper(scheme);
        public DbProviderFactory GetProviderFactory(SchemeKey scheme) => new Factory(this);
        public bool CanHandle(string scheme) => inner.CanHandle(scheme);
        public bool TryGetCapability<T>(SchemeKey scheme, out T? capability) where T : class
        {
            if (typeof(T) == typeof(SchemaDiscoveryCapability)) { capability = (T)(object)new SchemaDiscoveryCapability(Metadata); return true; }
            return inner.TryGetCapability(scheme, out capability);
        }
        public ResolvedConnection Resolve(ConnectionUri uri) => inner.Resolve(uri) with { ProviderFactory = new Factory(this) };
        public bool TryResolve(ConnectionUri uri, out ResolvedConnection? connection, out ConnectionResolutionDiagnostic? diagnostic)
        {
            var resolved = inner.TryResolve(uri, out connection, out diagnostic);
            if (resolved) connection = connection! with { ProviderFactory = new Factory(this) };
            return resolved;
        }
        private sealed class Factory(Registry owner) : DbProviderFactory
        {
            public override DbConnection CreateConnection() { var connection = new Connection(); owner.Connections.Add(connection); return connection; }
        }
    }
    internal sealed class Reader : ISchemaMetadataReader
    {
        internal bool Configured { get; private set; }
        public IReadOnlyDictionary<SchemaFeature, MetadataCoverage> Capabilities { get; } =
            Enum.GetValues<SchemaFeature>().ToDictionary(feature => feature, _ => MetadataCoverage.Available);
        public void ConfigureOwnedConnection(DbConnection connection)
        {
            NUnit.Framework.Assert.That(connection.State, NUnit.Framework.Is.EqualTo(ConnectionState.Closed));
            Configured = true;
        }
        public Task<SchemaMetadata> ReadAsync(DbConnection connection, DiscoveryOptions options, CancellationToken cancellationToken = default)
            => Task.FromResult(new SchemaMetadata([new(new("qa_oauth"), [new("id", 0, DbType.Int32, false)])], Capabilities, []));
    }
    internal sealed class Connection : DbConnection
    {
        internal bool WasDisposed { get; private set; }
        internal List<string> Statements { get; } = [];
        private ConnectionState state;
        [System.Diagnostics.CodeAnalysis.AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "app";
        public override string DataSource => "controlled";
        public override string ServerVersion => "1";
        public override ConnectionState State => state;
        public override void Open() => state = ConnectionState.Open;
        public override Task OpenAsync(CancellationToken cancellationToken) { cancellationToken.ThrowIfCancellationRequested(); Open(); return Task.CompletedTask; }
        public override void Close() => state = ConnectionState.Closed;
        public override void ChangeDatabase(string value) => throw new NotSupportedException();
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolation) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new Command(this);
        protected override void Dispose(bool disposing) { WasDisposed = true; Close(); base.Dispose(disposing); }
        private sealed class Command(Connection owner) : DbCommand
        {
            private readonly SqlCommand parameters = new();
            [System.Diagnostics.CodeAnalysis.AllowNull] public override string CommandText { get; set; } = "";
            public override int CommandTimeout { get; set; }
            public override CommandType CommandType { get; set; }
            public override bool DesignTimeVisible { get; set; }
            public override UpdateRowSource UpdatedRowSource { get; set; }
            protected override DbConnection? DbConnection { get; set; } = owner;
            protected override DbTransaction? DbTransaction { get; set; }
            protected override DbParameterCollection DbParameterCollection => parameters.Parameters;
            protected override DbParameter CreateDbParameter() => parameters.CreateParameter();
            public override void Cancel() { }
            public override void Prepare() { }
            public override int ExecuteNonQuery() { owner.Statements.Add(CommandText); return 1; }
            public override object ExecuteScalar() => 1;
            protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
            { owner.Statements.Add(CommandText); var table = new DataTable(); table.Columns.Add("value", typeof(int)); table.Rows.Add(1); return table.CreateDataReader(); }
            protected override void Dispose(bool disposing) { if (disposing) parameters.Dispose(); base.Dispose(disposing); }
        }
    }
}
