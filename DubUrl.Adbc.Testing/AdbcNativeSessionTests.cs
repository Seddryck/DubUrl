using System.Data.Common;
using Apache.Arrow;
using Apache.Arrow.Adbc;
using Apache.Arrow.Ipc;
using Apache.Arrow.Types;
using DubUrl.Mapping;
using Moq;
using NUnit.Framework;
using NativeConnection = Apache.Arrow.Adbc.AdbcConnection;

namespace DubUrl.Adbc.Testing;

[NonParallelizable]
public class AdbcNativeSessionTests
{
    private const string Provider = "DubUrl.Provider.DuckDb.Adbc";
    private Mock<AdbcDriver> driver = null!;
    private Mock<AdbcDatabase> database = null!;
    private Mock<NativeConnection> connection = null!;
    private Mock<AdbcStatement> statement = null!;
    private readonly List<string> disposed = [];
    private int driverCreations;

    [SetUp]
    public void SetUp()
    {
        disposed.Clear();
        driverCreations = 0;
        driver = new();
        database = new();
        connection = new();
        statement = new();
        statement.SetupAllProperties();
        driver.Setup(x => x.Open(It.IsAny<IReadOnlyDictionary<string, string>>())).Returns(database.Object);
        database.Setup(x => x.Connect(It.IsAny<IReadOnlyDictionary<string, string>>())).Returns(connection.Object);
        connection.Setup(x => x.CreateStatement()).Returns(statement.Object);
        statement.Setup(x => x.Dispose()).Callback(() => disposed.Add("statement"));
        connection.Setup(x => x.Dispose()).Callback(() => disposed.Add("connection"));
        database.Setup(x => x.Dispose()).Callback(() => disposed.Add("database"));
        driver.Setup(x => x.Dispose()).Callback(() => disposed.Add("driver"));
        DbProviderFactories.RegisterFactory(Provider, new AdbcProviderFactory(() => { driverCreations++; return driver.Object; }));
        DefaultRegistry.Refresh();
    }

    [TearDown]
    public void TearDown() => DbProviderFactories.UnregisterFactory(Provider);

    [TestCase("adbc+duckdb:///customer.db", "customer.db")]
    [TestCase("duckdb+adbc:///customer%20list.db", "customer list.db")]
    [TestCase("ADBC+DUCK://", ":memory:")]
    public void Open_UsesSharedUrlMappingAndReturnsApacheConnection(string url, string expected)
    {
        var connectionUrl = new AdbcConnectionUrl(url);
        Assert.That(driverCreations, Is.Zero);
        using var session = connectionUrl.Open();
        Assert.That(session.Connection, Is.SameAs(connection.Object));
        driver.Verify(x => x.Open(It.Is<IReadOnlyDictionary<string, string>>(p => p["path"] == expected)), Times.Once);
    }

    [Test]
    public void Open_CustomRegistry_ReusesRootPathAndDatabaseOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "native-adbc");
        var registry = new ProviderCatalogBuilder().WithRootPath(root).AddApplicationProviders().Build();
        using var session = new AdbcConnectionUrl("adbc+duckdb:///customer.db?access_mode=read_only", registry).Open();
        driver.Verify(x => x.Open(It.Is<IReadOnlyDictionary<string, string>>(p =>
            p["path"] == Path.Combine(root, "customer.db") && p["access_mode"] == "read_only")), Times.Once);
    }

    [Test]
    public void Dispose_ReleasesNativeResourcesOnceAndPreventsFurtherAccess()
    {
        var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
        session.Dispose();
        session.Dispose();
        Assert.That(disposed, Is.EqualTo(new[] { "connection", "database", "driver" }));
        Assert.Throws<ObjectDisposedException>(() => _ = session.Connection);
    }

    [Test]
    public void Dispose_ConnectionDisposalFailure_StillReleasesDatabaseAndDriver()
    {
        connection.Setup(x => x.Dispose()).Callback(() => disposed.Add("connection")).Throws(new InvalidOperationException("dispose failed"));
        var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
        Assert.Throws<InvalidOperationException>(session.Dispose);
        session.Dispose();
        Assert.That(disposed, Is.EqualTo(new[] { "connection", "database", "driver" }));
    }

    [Test]
    public void Open_DatabaseFailure_DisposesDriver()
    {
        driver.Setup(x => x.Open(It.IsAny<IReadOnlyDictionary<string, string>>())).Throws(new InvalidOperationException("open failed"));
        Assert.Throws<InvalidOperationException>(() => new AdbcConnectionUrl("adbc+duckdb://").Open());
        Assert.That(disposed, Is.EqualTo(new[] { "driver" }));
    }

    [Test]
    public void Open_ConnectionFailure_DisposesDatabaseAndDriver()
    {
        database.Setup(x => x.Connect(It.IsAny<IReadOnlyDictionary<string, string>>())).Throws(new InvalidOperationException("connect failed"));
        Assert.Throws<InvalidOperationException>(() => new AdbcConnectionUrl("adbc+duckdb://").Open());
        Assert.That(disposed, Is.EqualTo(new[] { "database", "driver" }));
    }

    [Test]
    public void ExecuteQuery_Failure_CallerScopesDisposeStatementAndSession()
    {
        statement.Setup(x => x.ExecuteQuery()).Throws(new InvalidOperationException("query failed"));
        Assert.Throws<InvalidOperationException>(() =>
        {
            using var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
            using var command = session.Connection.CreateStatement();
            command.ExecuteQuery();
        });
        Assert.That(disposed, Is.EqualTo(new[] { "statement", "connection", "database", "driver" }));
    }

    [Test]
    public async Task ExecuteQuery_PreservesStreamSchemaAndBatchesWithoutEagerReads()
    {
        var schema = new Schema.Builder().Field(new Field("id", Int64Type.Default, true)).Build();
        using var first = new RecordBatch(schema, [new Int64Array.Builder().Append(42).AppendNull().Build()], 2);
        using var second = new RecordBatch(schema, [new Int64Array.Builder().Append(99).Build()], 1);
        var stream = new Mock<IArrowArrayStream>();
        stream.SetupGet(x => x.Schema).Returns(schema);
        stream.SetupSequence(x => x.ReadNextRecordBatchAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<RecordBatch>(first))
            .Returns(new ValueTask<RecordBatch>(second))
            .Returns(new ValueTask<RecordBatch>((RecordBatch)null!));
        statement.Setup(x => x.ExecuteQuery()).Returns(new QueryResult(-1, stream.Object));

        using var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
        using var command = session.Connection.CreateStatement();
        var result = command.ExecuteQuery();
        using var returned = result.Stream!;
        Assert.That(returned, Is.SameAs(stream.Object));
        Assert.That(returned.Schema, Is.SameAs(schema));
        stream.Verify(x => x.ReadNextRecordBatchAsync(It.IsAny<CancellationToken>()), Times.Never);
        Assert.That(await returned.ReadNextRecordBatchAsync(), Is.SameAs(first));
        var ids = (Int64Array)first.Column(0);
        Assert.That(ids.GetValue(0), Is.EqualTo(42));
        Assert.That(ids.IsNull(1), Is.True);
        Assert.That(await returned.ReadNextRecordBatchAsync(), Is.SameAs(second));
        Assert.That(await returned.ReadNextRecordBatchAsync(), Is.Null);
        Assert.That(disposed, Is.Empty);
    }

    [Test]
    public void EarlyTermination_DisposesStreamBeforeStatementAndSession()
    {
        var stream = new Mock<IArrowArrayStream>();
        stream.Setup(x => x.Dispose()).Callback(() => disposed.Add("stream"));
        statement.Setup(x => x.ExecuteQuery()).Returns(new QueryResult(-1, stream.Object));
        using (var session = new AdbcConnectionUrl("adbc+duckdb://").Open())
        using (var command = session.Connection.CreateStatement())
        using (var returned = command.ExecuteQuery().Stream!)
            Assert.That(session.Connection, Is.SameAs(connection.Object));
        Assert.That(disposed, Is.EqualTo(new[] { "stream", "statement", "connection", "database", "driver" }));
    }

    [Test]
    public void BatchRead_Cancellation_ReachesStreamAndDisposesResources()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var stream = new Mock<IArrowArrayStream>();
        stream.Setup(x => x.Dispose()).Callback(() => disposed.Add("stream"));
        stream.Setup(x => x.ReadNextRecordBatchAsync(cancellation.Token))
            .Returns(ValueTask.FromCanceled<RecordBatch>(cancellation.Token));
        statement.Setup(x => x.ExecuteQuery()).Returns(new QueryResult(-1, stream.Object));
        Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            using var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
            using var command = session.Connection.CreateStatement();
            using var returned = command.ExecuteQuery().Stream!;
            await returned.ReadNextRecordBatchAsync(cancellation.Token);
        });
        stream.Verify(x => x.ReadNextRecordBatchAsync(cancellation.Token), Times.Once);
        Assert.That(disposed, Is.EqualTo(new[] { "stream", "statement", "connection", "database", "driver" }));
    }

    [Test]
    public void BatchRead_Failure_CallerScopesDisposeStreamAndSession()
    {
        var stream = new Mock<IArrowArrayStream>();
        stream.Setup(x => x.Dispose()).Callback(() => disposed.Add("stream"));
        stream.Setup(x => x.ReadNextRecordBatchAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.FromException<RecordBatch>(new InvalidOperationException("batch failed")));
        statement.Setup(x => x.ExecuteQuery()).Returns(new QueryResult(-1, stream.Object));
        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using var session = new AdbcConnectionUrl("adbc+duckdb://").Open();
            using var command = session.Connection.CreateStatement();
            using var returned = command.ExecuteQuery().Stream!;
            await returned.ReadNextRecordBatchAsync();
        });
        Assert.That(disposed, Is.EqualTo(new[] { "stream", "statement", "connection", "database", "driver" }));
    }

    [Test]
    public void Open_NonAdbcFactory_RejectsRowProviderBeforeCreatingDriver()
    {
        var rowFactory = new Mock<DbProviderFactory>();
        rowFactory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new DbConnectionStringBuilder());
        DbProviderFactories.RegisterFactory(Provider, rowFactory.Object);
        DefaultRegistry.Refresh();
        Assert.Throws<NotSupportedException>(() => new AdbcConnectionUrl("adbc+duckdb://").Open());
        Assert.That(driverCreations, Is.Zero);
    }
}
