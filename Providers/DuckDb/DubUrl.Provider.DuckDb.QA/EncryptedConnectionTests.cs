using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Runtime.Loader;
using DubUrl.Application;
using DubUrl.Authentication;
using DubUrl.BulkCopy;
using DubUrl.Discovery;
using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Discovery;
using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Provider.DuckDb.Native;
using DubUrl.Provider.DuckDb.Native.BulkCopy;
using DubUrl.Schema;
using DuckDB.NET.Data;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.QA;

[NonParallelizable, Category("DuckDB"), Category("AdoProvider"), Category("Authentication")]
public class EncryptedConnectionTests
{
    private string path = null!;
    private string key = null!;
    private ISchemeRegistry registry = null!;
    private DbProviderFactory? previous;
    private bool hadFactory;
    private string Url => "duckdb:///" + path.Replace('\\', '/').Replace("%", "%25").Replace("#", "%23").Replace(" ", "%20");
    private ConnectionUrl Target(bool strategy = false)
        => strategy ? new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(key))
            : new ConnectionUrl(Url + "?ENCRYPTION_KEY=" + Uri.EscapeDataString(key), registry);

    [SetUp]
    public void Setup()
    {
        hadFactory = DbProviderFactories.TryGetFactory("DuckDB.NET.Data", out previous);
        DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDBClientFactory.Instance);
        var builder = new ProviderCatalogBuilder();
        builder.AddModule(new AttributeProviderModule(typeof(DuckdbMapper).Assembly));
        registry = builder.Build();
        path = Path.Combine(Path.GetTempPath(), "duburl-encrypted-' %#+-" + Guid.NewGuid().ToString("N") + ".duckdb");
        key = Guid.NewGuid().ToString("N") + "%2B +&'\";--";
        using var setup = new DuckDBConnection("DataSource=:memory:");
        setup.Open();
        Execute(setup, "ATTACH " + EncryptedDuckDbConnection.Literal(path) + " AS secured (ENCRYPTION_KEY " + EncryptedDuckDbConnection.Literal(key) + "); USE secured");
        Execute(setup, "CREATE TABLE sample(id INTEGER PRIMARY KEY, title VARCHAR); INSERT INTO sample VALUES(1,'first'); CREATE INDEX ix_title ON sample(title); CREATE VIEW sample_view AS SELECT id,title FROM sample");
    }

    [TearDown]
    public void Cleanup()
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".wal")) File.Delete(path + ".wal");
        if (hadFactory) DbProviderFactories.RegisterFactory("DuckDB.NET.Data", previous!);
        else DbProviderFactories.UnregisterFactory("DuckDB.NET.Data");
    }

    [TestCase(false, "caller")]
    [TestCase(true, "caller")]
    [TestCase(false, "caller-async")]
    [TestCase(true, "caller-async")]
    [TestCase(false, "helper")]
    [TestCase(true, "helper")]
    [TestCase(false, "helper-async")]
    [TestCase(true, "helper-async")]
    public async Task AllEntryPaths_OpenEncryptedFileAndReinitialize(bool strategy, string entry)
    {
        var target = Target(strategy);
        DbConnection connection;
        var events = 0;
        void Subscribe(DbConnection prepared)
        {
            prepared.StateChange += (_, e) =>
            {
                if (e.CurrentState != ConnectionState.Open) return;
                events++;
                Assert.That(Scalar((DuckDBConnection)prepared, "SELECT current_database()"), Is.EqualTo("secured"));
                Assert.That(Scalar((DuckDBConnection)prepared, "SELECT count(*) FROM sample"), Is.EqualTo(1L));
            };
        }
        switch (entry)
        {
            case "caller":
                connection = (DbConnection)target.Connect();
                Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
                Subscribe(connection); connection.Open(); break;
            case "caller-async":
                connection = await target.ConnectAsync();
                Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
                Subscribe(connection); await connection.OpenAsync(); break;
            case "helper": connection = (DbConnection)target.Open(); break;
            default: connection = await target.OpenAsync(); break;
        }
        using (connection)
        {
            Assert.That(connection, Is.InstanceOf<DuckDBConnection>());
            Assert.That(Scalar((DuckDBConnection)connection, "SELECT title FROM sample"), Is.EqualTo("first"));
            Assert.That(connection.ConnectionString, Does.Not.Contain(key));
            if (entry.StartsWith("caller")) Assert.That(events, Is.EqualTo(1));
            connection.Close(); connection.Open();
            if (entry.StartsWith("caller")) Assert.That(events, Is.EqualTo(2));
        }
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Test]
    public async Task NativeSchemaCommandsTransactionsAndTypedBulkCopy_WorkOnEncryptedCatalog()
    {
        using (var connection = (DuckDBConnection)Target().Open())
        {
            Execute(connection, "CREATE TABLE memory.main.decoy(id INTEGER)");
            using var command = connection.CreateCommand();
            Assert.That(command.Connection, Is.SameAs(connection));
            using (var transaction = connection.BeginTransaction())
            {
                Execute(connection, "INSERT INTO sample VALUES(99,'rollback')");
                transaction.Rollback();
            }
            Assert.That(Scalar(connection, "SELECT count(*) FROM sample WHERE id=99"), Is.EqualTo(0L));
            foreach (var collection in new[] { "Tables", "Columns", "ForeignKeys", "Indexes" })
            {
                var metadata = connection.GetSchema(collection);
                Assert.That(metadata.Rows.Cast<DataRow>().All(row => Equals(row[0], "secured")), Is.True);
            }
            string?[] restrictions = [null];
            Assert.That(connection.GetSchema("Tables", restrictions).Rows.Count, Is.GreaterThan(0));
            Assert.That(restrictions[0], Is.Null);
            Assert.That(connection.GetSchema("Tables", ["memory"]).Rows.Cast<DataRow>().Any(row => Equals(row["table_name"], "decoy")), Is.True);
            Assert.That(connection.GetSchema().Rows.Count, Is.GreaterThan(0));
            var discovery = await new SchemaInspector().InspectAsync(connection, new DuckDbMetadataReader(), new DiscoveryOptions
            { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.Indexes, SchemaFeature.Views] });
            Assert.That(discovery.IsSuccess && discovery.IsComplete, Is.True);
            Assert.That(discovery.Schema!.Tables.Values.Select(t => t.Name), Is.EqualTo(new[] { "sample" }));
            Assert.That(discovery.Schema.Indexes.Values.Any(i => i.Name == "ix_title"), Is.True);
            Assert.That(discovery.ViewInventory, Has.Count.EqualTo(1));
        }
        using var data = new DataTable();
        data.Columns.Add("id", typeof(int)); data.Columns.Add("title", typeof(string));
        data.Rows.Add(2, "bulk-copy"); data.Rows.Add(3, DBNull.Value);
        using var reader = data.CreateDataReader();
        // Exercise lazy provider activation as well as the typed adapter.
        using (var engine = Target().BulkCopyProvider!.Create(Target(), BulkCopyDependencyResolver.Default))
            engine.Write("sample", reader);
        Assert.That(reader.IsClosed, Is.False);
        using var check = (DuckDBConnection)Target().Open();
        Assert.That(Scalar(check, "SELECT count(*) FROM sample WHERE id=2 AND title='bulk-copy' OR id=3 AND title IS NULL"), Is.EqualTo(2L));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void WrongKey_NoOpenEventAndSafeFailure(bool strategy)
    {
        var correct = key;
        key = Guid.NewGuid().ToString("N");
        using var connection = (DbConnection)Target(strategy).Connect();
        var events = 0; connection.StateChange += (_, _) => events++;
        var error = Assert.Throws<ConnectionAuthenticationException>(() => connection.Open());
        Assert.That(error!.InnerException, Is.Null);
        Assert.That(error.ToString(), Does.Not.Contain(key).And.Not.Contain(correct).And.Not.Contain("ATTACH"));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(events, Is.Zero);
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Test]
    public void MissingKey_CannotOpenEncryptedFileWithoutStrategy()
    {
        using var connection = new DuckDBConnection(new DuckDBConnectionStringBuilder { DataSource = path }.ConnectionString);
        Assert.Throws<DuckDB.NET.Data.DuckDBException>(() => connection.Open());
    }

    [Test]
    public async Task MissingFile_IsNotCreatedDuringPreparationOrOpen()
    {
        File.Delete(path);
        var target = Target();
        _ = target.Parse(); target.ValidateAuthentication(); target.ValidateConfiguration();
        using var connection = target.Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(File.Exists(path), Is.False);
        Assert.Throws<FileNotFoundException>(() => connection.Open());
        Assert.That(File.Exists(path), Is.False);
        Assert.That((await new ConnectionCheckService().CheckAsync(target, new())).Success, Is.False);
    }

    [Test]
    public void ReadOnly_AppliesToAttachedFileAndSurvivesReopen()
    {
        using var connection = (DuckDBConnection)Target().WithReadOnly().Open();
        for (var cycle = 0; cycle < 2; cycle++)
        {
            Assert.That(Scalar(connection, "SELECT count(*) FROM sample"), Is.EqualTo(1L));
            Assert.Throws<DuckDB.NET.Data.DuckDBException>(() => Execute(connection, "INSERT INTO sample VALUES(2,'forbidden')"));
            connection.Close(); connection.Open();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DeferredSource_ConnectingIsVisibleUntilSecretResolutionCompletes(bool cancel)
    {
        var source = new PausedSource(key);
        var target = new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(source));
        await using var connection = await target.ConnectAsync();
        Assert.That(source.Calls, Is.Zero);
        var events = 0; connection.StateChange += (_, _) => events++;
        using var cancellation = new CancellationTokenSource();
        var opening = connection.OpenAsync(cancellation.Token);
        await source.Entered.Task;
        Assert.That(((DuckDBConnection)connection).State, Is.EqualTo(ConnectionState.Connecting));
        Assert.That(events, Is.Zero);
        Assert.Throws<InvalidOperationException>(() => Scalar((DuckDBConnection)connection, "SELECT 1"));
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.CatchAsync<OperationCanceledException>(async () => await opening);
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
            Assert.That(events, Is.Zero);
        }
        else
        {
            source.Release.TrySetResult(); await opening;
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
            Assert.That(events, Is.EqualTo(1));
        }
        Assert.That(source.Disposed, Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CloseOrDisposeDuringResolution_CannotResurrectConnection(bool dispose)
    {
        var source = new PausedSource(key);
        using var connection = (DbConnection)new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(source)).Connect();
        var opening = connection.OpenAsync(); await source.Entered.Task;
        if (dispose) connection.Dispose(); else connection.Close();
        source.Release.TrySetResult();
        await Assert.CatchAsync<OperationCanceledException>(async () => await opening);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(source.Disposed, Is.False);
    }

    [Test]
    public void AsyncOnlySource_RejectsSynchronousOpeningSafely()
    {
        var source = new PausedSource(key);
        using var connection = new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(source)).Connect();
        var error = Assert.Throws<ConnectionAuthenticationException>(() => connection.Open());
        Assert.That(error!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.SynchronousCredentialsRequired));
        Assert.That(source.Calls, Is.Zero);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public async Task PreCancelledOpen_DoesNotAcquireSecret()
    {
        var source = new PausedSource(key);
        using var connection = new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(source)).Connect();
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await ((DbConnection)connection).OpenAsync(cancellation.Token));
        Assert.That(source.Calls, Is.Zero);
        await Task.CompletedTask;
    }

    [TestCase("opened", false)]
    [TestCase("attached", false)]
    [TestCase("opened", true)]
    [TestCase("attached", true)]
    public async Task NativeInitialization_HidesStateAndEvents(string stage, bool asynchronous)
    {
        using var reached = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var connection = new EncryptedDuckDbConnection(new DuckDBConnectionStringBuilder { DataSource = path }.ConnectionString, new(key), step =>
        {
            if (step != stage) return;
            reached.Set();
            if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException();
        });
        var events = 0; connection.StateChange += (_, _) => Interlocked.Increment(ref events);
        var opening = Task.Run(async () => { if (asynchronous) await connection.OpenAsync(); else connection.Open(); });
        try
        {
            Assert.That(reached.Wait(TimeSpan.FromSeconds(15)), Is.True);
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(((DbConnection)connection).State, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(((DuckDBConnection)connection).State, Is.EqualTo(ConnectionState.Connecting));
            Assert.That(events, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => Scalar(connection, "SELECT 1"));
        }
        finally { release.Set(); }
        await opening;
        Assert.That(events, Is.EqualTo(1));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task FailureOrCancellationAfterAttach_CleansUpAndRedacts(bool cancel)
    {
        using var cancellation = new CancellationTokenSource();
        using var connection = new EncryptedDuckDbConnection(new DuckDBConnectionStringBuilder { DataSource = path }.ConnectionString, new(key), step =>
        {
            if (step != "attached") return;
            if (cancel) cancellation.Cancel();
            else throw new InvalidOperationException("ATTACH " + key);
        });
        var events = 0; connection.StateChange += (_, _) => events++;
        if (cancel) await Assert.CatchAsync<OperationCanceledException>(async () => await connection.OpenAsync(cancellation.Token));
        else
        {
            var error = Assert.Throws<ConnectionAuthenticationException>(() => connection.Open());
            Assert.That(error!.InnerException, Is.Null);
            Assert.That(error.ToString(), Does.Not.Contain(key).And.Not.Contain("ATTACH"));
        }
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(events, Is.Zero);
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Test]
    public async Task ConnectionCheckAndSchemaDeployment_UsePreparedOpening()
    {
        var result = await new ConnectionCheckService().CheckAsync(Target(), new ConnectionCheckOptions { Table = "sample" });
        Assert.That(result.Success, Is.True);
        var inspected = await new SchemaInspector().InspectAsync(Target());
        Assert.That(inspected.IsSuccess, Is.True);
        new SchemaScriptDeployer().DeploySchema(Target(), "CREATE TABLE deployed(id INTEGER)");
        Assert.That((await new SchemaInspector().InspectAsync(Target())).Schema!.Tables.Values.Any(t => t.Name == "deployed"), Is.True);
    }

    [Test]
    public void PlaintextFile_WithKeyDoesNotFallBackToUnencryptedOpening()
    {
        File.Delete(path);
        using (var plain = new DuckDBConnection(new DuckDBConnectionStringBuilder { DataSource = path }.ConnectionString))
        { plain.Open(); Execute(plain, "CREATE TABLE sample(id INTEGER)"); }
        Assert.Throws<ConnectionAuthenticationException>(() => Target().Open());
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SourceResolution_IsRepeatedPerOpenAndFailuresStaySafe(bool asynchronous)
    {
        var source = new RotatingSource(key);
        var target = new ConnectionUrl(Url, registry).WithAuthentication(ConnectionAuthentication.FilePassword(source));
        using var connection = asynchronous ? await target.ConnectAsync() : (DbConnection)target.Connect();
        Assert.That(source.Calls, Is.Zero);
        if (asynchronous) await connection.OpenAsync(); else connection.Open();
        connection.Close();
        source.Fail = true;
        var failure = asynchronous
            ? await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await connection.OpenAsync())
            : Assert.Throws<ConnectionAuthenticationException>(() => connection.Open());
        Assert.That(failure!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.CredentialResolutionFailed));
        Assert.That(failure.ToString(), Does.Not.Contain(key));
        Assert.That(failure.InnerException, Is.Null);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        source.Fail = false; source.Secret = "";
        var empty = asynchronous
            ? await Assert.ThrowsAsync<ConnectionAuthenticationException>(async () => await connection.OpenAsync())
            : Assert.Throws<ConnectionAuthenticationException>(() => connection.Open());
        Assert.That(empty!.Code, Is.EqualTo(AuthenticationDiagnosticCodes.MissingCredentials));
        source.Secret = key;
        if (asynchronous) await connection.OpenAsync(); else connection.Open();
        Assert.That(source.Calls, Is.EqualTo(4));
        connection.Dispose();
        Assert.That(source.Disposed, Is.False);
    }

    [Test]
    public void ConcurrentAttachments_FailSafelyAndRecoverAfterOwnerCloses()
    {
        using var first = (DuckDBConnection)Target().Open();
        using var second = (DbConnection)Target(true).Connect();
        Assert.Throws<ConnectionAuthenticationException>(() => second.Open());
        Assert.That(second.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(Scalar(first, "SELECT title FROM sample"), Is.EqualTo("first"));
        // Reproduce the same restriction through the unmodified native provider.
        using (var native = new DuckDBConnection("DataSource=:memory:"))
        {
            native.Open(); using var command = native.CreateCommand();
            command.CommandText = "ATTACH " + EncryptedDuckDbConnection.Literal(path) + " AS probe (ENCRYPTION_KEY ?)";
            command.Parameters.Add(new DuckDBParameter { Value = key });
            var error = Assert.Throws<DuckDB.NET.Data.DuckDBException>(() => command.ExecuteNonQuery());
            var safe = error!.Message.Replace(key, "[REDACTED]", StringComparison.Ordinal);
            TestContext.Out.WriteLine("Native concurrent attachment restriction: " + safe);
        }
        first.Close();
        second.Open();
        Assert.That(Scalar((DuckDBConnection)second, "SELECT title FROM sample"), Is.EqualTo("first"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AttachGrammar_BindsKeyButRequiresLiteralPath(bool bindKey)
    {
        using var connection = new DuckDBConnection("DataSource=:memory:"); connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = bindKey
            ? "ATTACH " + EncryptedDuckDbConnection.Literal(path) + " AS probe (ENCRYPTION_KEY ?)"
            : "ATTACH ? AS probe (ENCRYPTION_KEY 'placeholder')";
        command.Parameters.Add(new DuckDBParameter { Value = bindKey ? key : path });
        if (bindKey)
        {
            Assert.DoesNotThrow(() => command.ExecuteNonQuery());
            Assert.That(Scalar(connection, "SELECT count(*) FROM probe.main.sample"), Is.EqualTo(1L));
        }
        else Assert.Throws<DuckDB.NET.Data.DuckDBException>(() => command.ExecuteNonQuery());
    }

    private sealed class RotatingSource(string secret) : ISynchronousFileSecretCredentialSource, IDisposable
    {
        public string Secret { get; set; } = secret;
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public bool Disposed { get; private set; }
        public FileSecretCredentials Resolve(AuthenticationContext context)
        { Calls++; if (Fail) throw new InvalidOperationException(Secret); return new(Secret); }
        public ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); return new(Resolve(context)); }
        public void Dispose() => Disposed = true;
    }

    [Test]
    public async Task IsolatedNativeDriver_UsesSameContextForSubclassAndBulkCopy()
    {
        var isolated = new DriverLoadContext();
        try
        {
            var driver = isolated.LoadFromAssemblyName(new("DuckDB.NET.Data"));
            var factory = (DbProviderFactory)driver.GetType("DuckDB.NET.Data.DuckDBClientFactory")!.GetField("Instance")!.GetValue(null)!;
            DbProviderFactories.RegisterFactory("DuckDB.NET.Data", factory);
            var builder = new ProviderCatalogBuilder();
            builder.AddModule(new AttributeProviderModule(isolated.LoadFromAssemblyName(new("DubUrl.Provider.DuckDb"))));
            var target = new ConnectionUrl(Url + "?ENCRYPTION_KEY=" + Uri.EscapeDataString(key), builder.Build());
            await using (var connection = await target.OpenAsync())
            {
                Assert.That(AssemblyLoadContext.GetLoadContext(connection.GetType().Assembly), Is.SameAs(isolated));
                Assert.That(AssemblyLoadContext.GetLoadContext(connection.GetType().BaseType!.Assembly), Is.SameAs(isolated));
                using var command = connection.CreateCommand(); command.CommandText = "SELECT count(*) FROM sample";
                Assert.That(await command.ExecuteScalarAsync(), Is.EqualTo(1L));
            }
            using var data = new DataTable(); data.Columns.Add("id", typeof(int)); data.Columns.Add("title", typeof(string)); data.Rows.Add(7, "isolated");
            using var reader = data.CreateDataReader();
            using (var engine = target.BulkCopyProvider!.Create(target, BulkCopyDependencyResolver.Default)) engine.Write("sample", reader);
            await using var check = await target.OpenAsync();
            using var query = check.CreateCommand(); query.CommandText = "SELECT title FROM sample WHERE id=7";
            Assert.That(await query.ExecuteScalarAsync(), Is.EqualTo("isolated"));
        }
        finally
        {
            DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDBClientFactory.Instance);
            isolated.Unload();
        }
    }

    private sealed class DriverLoadContext() : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver resolver = new(typeof(EncryptedConnectionTests).Assembly.Location);
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(ConnectionUrl).Assembly.GetName().Name) return typeof(ConnectionUrl).Assembly;
            var file = resolver.ResolveAssemblyToPath(name);
            return file is null ? null : LoadFromAssemblyPath(file);
        }
        protected override IntPtr LoadUnmanagedDll(string name)
        {
            var file = resolver.ResolveUnmanagedDllToPath(name);
            return file is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(file);
        }
    }

    private sealed class PausedSource(string secret) : IFileSecretCredentialSource, IDisposable
    {
        public int Calls { get; private set; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async ValueTask<FileSecretCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        { Calls++; Entered.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); return new(secret); }
        public void Dispose() => Disposed = true;
    }

    private static void Execute(DuckDBConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; command.ExecuteNonQuery(); }
    private static object? Scalar(DuckDBConnection connection, string sql)
    { using var command = connection.CreateCommand(); command.CommandText = sql; return command.ExecuteScalar(); }
}
