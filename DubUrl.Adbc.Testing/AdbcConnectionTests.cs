using System.Data;
using System.Data.Common;
using Apache.Arrow;
using Apache.Arrow.Adbc;
using Apache.Arrow.Ipc;
using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Sql;
using Moq;
using NUnit.Framework;
using NativeConnection = Apache.Arrow.Adbc.AdbcConnection;

namespace DubUrl.Adbc.Testing;

[NonParallelizable]
public class AdbcConnectionTests
{
    private const string Provider = "DubUrl.Provider.DuckDb.Adbc";
    private Mock<AdbcDriver> driver = null!;
    private Mock<AdbcDatabase> database = null!;
    private Mock<NativeConnection> nativeConnection = null!;
    private Mock<AdbcStatement> statement = null!;
    private readonly List<string> disposed = [];

    [SetUp]
    public void SetUp()
    {
        disposed.Clear();
        driver = new();
        database = new();
        nativeConnection = new();
        statement = new();
        statement.SetupAllProperties();
        driver.Setup(x => x.Open(It.IsAny<IReadOnlyDictionary<string, string>>())).Returns(database.Object);
        database.Setup(x => x.Connect(It.IsAny<IReadOnlyDictionary<string, string>>())).Returns(nativeConnection.Object);
        nativeConnection.Setup(x => x.CreateStatement()).Returns(statement.Object);
        nativeConnection.Setup(x => x.Dispose()).Callback(() => disposed.Add("connection"));
        database.Setup(x => x.Dispose()).Callback(() => disposed.Add("database"));
        driver.Setup(x => x.Dispose()).Callback(() => disposed.Add("driver"));
        DbProviderFactories.RegisterFactory(Provider, new AdbcProviderFactory(() => driver.Object));
        DefaultRegistry.Refresh();
    }

    [TearDown]
    public void TearDown() => DbProviderFactories.UnregisterFactory(Provider);

    [TestCase("adbc+duckdb:///customer.db", "customer.db")]
    [TestCase("duckdb+adbc:///customer.db", "customer.db")]
    [TestCase("ADBC+DUCK:///customer.db", "customer.db")]
    [TestCase("adbc+duckdb://", ":memory:")]
    [TestCase("adbc+duckdb://memory", ":memory:")]
    [TestCase("adbc+duckdb:///:memory:", ":memory:")]
    [TestCase("adbc+duckdb:///data/customer%20list.db", "data/customer list.db")]
    public void Open_Url_UsesDuckdbPath(string url, string expected)
    {
        using var connection = new ConnectionUrl(url).Open();
        driver.Verify(x => x.Open(It.Is<IReadOnlyDictionary<string, string>>(p =>
            p["path"] == expected.Replace('/', Path.DirectorySeparatorChar) && !p.ContainsKey("DataSource"))), Times.Once);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public void Parse_RootAndOptions_PreservesPathAndOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "duburl");
        var registry = new ProviderCatalogBuilder().WithRootPath(root).AddApplicationProviders().Build();
        var url = new ConnectionUrl("adbc+duckdb:///customer.db?access_mode=read_only", registry);
        var parsed = new DbConnectionStringBuilder { ConnectionString = url.Parse() };
        Assert.Multiple(() =>
        {
            Assert.That(parsed["path"], Is.EqualTo(Path.Combine(root, "customer.db")));
            Assert.That(parsed["access_mode"], Is.EqualTo("read_only"));
            Assert.That(url.Dialect, Is.TypeOf<DuckDbDialect>());
            Assert.That(url.Connectivity.Alias, Is.EqualTo("adbc"));
        });
    }

    [Test]
    public void Connect_DoesNotLoadDriverUntilOpened()
    {
        using var connection = new ConnectionUrl("adbc+duckdb:///customer.db").Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        driver.Verify(x => x.Open(It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Test]
    public void Dispose_ReleasesResourcesInOrderOnce()
    {
        var connection = new ConnectionUrl("adbc+duckdb://").Open();
        connection.Close();
        connection.Dispose();
        Assert.That(disposed, Is.EqualTo(new[] { "connection", "database", "driver" }));
    }

    [Test]
    public void Open_ConnectionFailure_ReleasesDatabaseAndDriver()
    {
        database.Setup(x => x.Connect(It.IsAny<IReadOnlyDictionary<string, string>>())).Throws(new InvalidOperationException("connect failed"));
        Assert.Throws<InvalidOperationException>(() => new ConnectionUrl("adbc+duckdb://").Open());
        Assert.That(disposed, Is.EqualTo(new[] { "database", "driver" }));
    }

    [Test]
    public void Open_DatabaseFailure_ReleasesDriver()
    {
        driver.Setup(x => x.Open(It.IsAny<IReadOnlyDictionary<string, string>>())).Throws(new InvalidOperationException("open failed"));
        Assert.Throws<InvalidOperationException>(() => new ConnectionUrl("adbc+duckdb://").Open());
        Assert.That(disposed, Is.EqualTo(new[] { "driver" }));
    }

    [Test]
    public void Close_AllowsReopeningWithFreshDriver()
    {
        var count = 0;
        DbProviderFactories.RegisterFactory(Provider, new AdbcProviderFactory(() => { count++; return driver.Object; }));
        DefaultRegistry.Refresh();
        using var connection = new ConnectionUrl("adbc+duckdb://").Connect();
        connection.Open();
        connection.Close();
        connection.Open();
        Assert.That(count, Is.EqualTo(2));
    }

    private void ReturnRows(params long?[] values)
    {
        var schema = new Schema.Builder().Field(new Field("value", Apache.Arrow.Types.Int64Type.Default, true)).Build();
        var column = new Int64Array.Builder();
        foreach (var value in values)
        {
            if (value.HasValue)
                column.Append(value.Value);
            else
                column.AppendNull();
        }
        var batch = new RecordBatch(schema, [column.Build()], values.Length);
        var stream = new Mock<IArrowArrayStream>();
        stream.SetupGet(x => x.Schema).Returns(schema);
        stream.SetupSequence(x => x.ReadNextRecordBatchAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<RecordBatch>(batch))
            .Returns(new ValueTask<RecordBatch>((RecordBatch)null!));
        statement.Setup(x => x.ExecuteQuery()).Returns(new QueryResult(values.Length, stream.Object));
    }

    [Test]
    public void ExecuteReader_ArrowResults_AreReadableThroughAdoNet()
    {
        ReturnRows(42, null);
        using var connection = new ConnectionUrl("adbc+duckdb://").Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM example";
        using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.GetInt64(0), Is.EqualTo(42));
        Assert.That(reader.Read(), Is.True);
        Assert.That(reader.IsDBNull(0), Is.True);
        Assert.That(reader.Read(), Is.False);
        Assert.That(statement.Object.SqlQuery, Is.EqualTo(command.CommandText));
    }

    [Test]
    public void ExecuteReader_CloseConnection_ReleasesAllNativeResources()
    {
        ReturnRows(42);
        using var connection = new ConnectionUrl("adbc+duckdb://").Open();
        using var command = connection.CreateCommand();
        using (var reader = command.ExecuteReader(CommandBehavior.CloseConnection))
            Assert.That(reader.Read(), Is.True);
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        Assert.That(disposed, Is.EqualTo(new[] { "connection", "database", "driver" }));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ReadScalar_DatabaseUrl_UsesAdbcProvider(bool isNull)
    {
        ReturnRows(isNull ? null : 42);
        var result = new DatabaseUrl("adbc+duckdb://").ReadScalar<long?>("SELECT value FROM example");
        Assert.That(result, Is.EqualTo(isNull ? null : (long?)42));
        Assert.That(disposed, Is.EqualTo(new[] { "connection", "database", "driver" }));
    }

    [Test]
    public void ReadScalar_EmptyResult_ReturnsNull()
    {
        ReturnRows();
        Assert.That(new DatabaseUrl("adbc+duckdb://").ReadScalar("SELECT value FROM example"), Is.Null);
    }

    [Test]
    public void Parse_WithoutRegisteredProvider_DoesNotAdvertiseAdbcScheme()
    {
        DbProviderFactories.UnregisterFactory(Provider);
        Assert.That(DefaultRegistry.Refresh().CanHandle("adbc+duckdb"), Is.False);
    }
}
