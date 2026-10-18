using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Data;
using System.Data.Common;

namespace DubUrl.Testing;

public class DatabaseConnectionSourceTest
{
    [TestCase("url")]
    [TestCase("factory")]
    [TestCase("connection")]
    [TestCase("factory-with-provisioners")]
    [TestCase("micro-orm")]
    public void ExistingConstructors_KeepProviderManagedBehaviorForDuckDb(string constructor)
    {
        DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDB.NET.Data.DuckDBClientFactory.Instance);
        const string url = "duckdb://memory";
        var factory = new ConnectionUrlFactory(DubUrl.Mapping.SchemeRegistryBuilder.GetDefault());
        using var database = constructor switch
        {
            "url" => new DatabaseUrl(url),
            "factory" => new DatabaseUrl(factory, url),
            "connection" => new DatabaseUrl(factory.Instantiate(url), new(), NullQueryLogger.Instance),
            "factory-with-provisioners" => new DatabaseUrl(factory, new(), url),
            _ => new DubUrl.MicroOrm.DatabaseUrl(factory.Instantiate(url), new(), new DubUrl.MicroOrm.ReflectionCache(), NullQueryLogger.Instance)
        };

        using (database.ExecuteReader("CREATE TABLE connection_default_test (value INTEGER)")) { }
        // A new connection gets a fresh in-memory database. Auto would retain the table.
        var tables = database.ReadScalar("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'connection_default_test'");
        Assert.That(Convert.ToInt32(tables), Is.Zero);
    }

    [Test]
    public async Task InjectedDefaultSource_UsesAutomaticRetentionForDuckDb()
    {
        DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDB.NET.Data.DuckDBClientFactory.Instance);
        await using var source = new ConnectionSourceUrl("duckdb://memory").Create();
        await using var database = new DatabaseUrl(source);
        using (database.ExecuteReader("CREATE TABLE connection_default_test (value INTEGER)")) { }
        var tables = await database.ReadScalarAsync("SELECT COUNT(*) FROM information_schema.tables WHERE table_name = 'connection_default_test'");
        Assert.That(Convert.ToInt32(tables), Is.EqualTo(1));
    }

    private sealed class Rig : IDisposable
    {
        public readonly Mock<DbConnection> Connection = new();
        public readonly List<Mock<DbCommand>> Commands = [];
        public readonly List<Mock<DbDataReader>> Readers = [];
        public readonly Mock<CommandProvisionerFactory> Provisioners = new();
        public readonly ConnectionSource Source;
        public bool FailCreate, FailProvision, FailExecute, FailRead, FailMap;
        public int Rows = 2;
        public int Opens;

        public Rig(ConnectionReuseMode mode = ConnectionReuseMode.RetainedExclusive)
        {
            var state = ConnectionState.Closed;
            Connection.SetupGet(c => c.State).Returns(() => state);
            Connection.Setup(c => c.Open()).Callback(() => { state = ConnectionState.Open; Opens++; });
            Connection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>())).Callback(() => { state = ConnectionState.Open; Opens++; }).Returns(Task.CompletedTask);
            Connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
            Connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(() =>
            {
                if (FailCreate) throw new InvalidOperationException("Create failed.");
                var command = new Mock<DbCommand>();
                command.SetupGet(c => c.CommandText).Returns("SELECT 42");
                command.Setup(c => c.ExecuteScalar()).Returns(() => FailExecute ? throw new InvalidOperationException() : 42);
                command.Setup(c => c.ExecuteScalarAsync(It.IsAny<CancellationToken>())).Returns(() =>
                    FailExecute ? Task.FromException<object?>(new InvalidOperationException()) : Task.FromResult<object?>(42));
                command.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
                command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>()).Returns(CreateReader);
                command.Protected().Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
                    .Returns(() => Task.FromResult(CreateReader()));
                Commands.Add(command);
                return command.Object;
            });
            Provisioners.Setup(p => p.Instantiate(It.IsAny<ICommandProvider>(), It.IsAny<ConnectionUrl>()))
                .Returns(() => FailProvision ? throw new InvalidOperationException("Provision failed.") : []);
            Source = ConnectionSourceTest.CreateUrl(() => Connection.Object).Create(new() { ReuseMode = mode });
        }

        private DbDataReader CreateReader()
        {
            if (FailExecute) throw new InvalidOperationException("Execute failed.");
            var reader = new Mock<DbDataReader>();
            int reads = 0;
            bool Read() => FailRead ? throw new InvalidOperationException("Read failed.") : reads++ < Rows;
            reader.Setup(r => r.Read()).Returns(Read);
            reader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>())).Returns(() => Task.FromResult(Read()));
            reader.SetupGet(r => r.FieldCount).Returns(1);
            reader.Setup(r => r.GetName(0)).Returns("Value");
            reader.Setup(r => r.GetValue(0)).Returns(() => FailMap ? throw new InvalidOperationException("Map failed.") : 42);
            reader.Setup(r => r.GetFieldType(0)).Returns(typeof(int));
            reader.Setup(r => r["Value"]).Returns(42);
            reader.Setup(r => r.DisposeAsync()).Returns(ValueTask.CompletedTask);
            Readers.Add(reader);
            return reader.Object;
        }

        public DatabaseUrl Database() => new(Source, Provisioners.Object, NullQueryLogger.Instance);
        public DubUrl.MicroOrm.DatabaseUrl MicroOrm() => new(Source, Provisioners.Object, new DubUrl.MicroOrm.ReflectionCache(), NullQueryLogger.Instance);
        public void Dispose() => Source.Dispose();
    }

    public sealed class Row { public int Value { get; set; } }

    [Test]
    public async Task SequentialQueries_BorrowSourceAndRetainConnection()
    {
        using var rig = new Rig();
        using (var database = rig.Database())
        {
            Assert.That(database.ReadScalar<int>("SELECT 42"), Is.EqualTo(42));
            Assert.That(await database.ReadScalarAsync<int>("SELECT 42"), Is.EqualTo(42));
        }
        Assert.That(rig.Opens, Is.EqualTo(1));
        rig.Connection.Protected().Verify("Dispose", Times.Never(), ItExpr.IsAny<bool>());
        rig.Connection.Verify(c => c.DisposeAsync(), Times.Never);
        using var lease = rig.Source.Acquire();
    }

    [Test]
    public void DisposedBorrower_RejectsUseWithoutDisposingSharedSource()
    {
        using var rig = new Rig();
        var database = rig.Database();
        var lazy = database.ReadMultiple("SELECT 42");
        database.Dispose();
        database.Dispose();
        Assert.Throws<ObjectDisposedException>(() => database.ReadScalar("SELECT 42"));
        Assert.Throws<ObjectDisposedException>(() => lazy.ToList());
        using var other = rig.Database();
        Assert.That(other.ReadScalar<int>("SELECT 42"), Is.EqualTo(42));
    }

    [Test]
    public void LazyEnumeration_AcquiresOnMoveNextAndReleasesOnEarlyDispose()
    {
        using var rig = new Rig();
        using var database = rig.Database();
        var enumerable = database.ReadMultiple("SELECT 42");
        Assert.That(rig.Opens, Is.Zero);
        using (var enumerator = enumerable.GetEnumerator())
            Assert.That(enumerator.MoveNext(), Is.True);
        using var lease = rig.Source.Acquire();
        rig.Readers[0].Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        rig.Commands[0].Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
    }

    [Test]
    public async Task RawReader_KeepsLeaseUntilClose_AndReleasesExactlyOnce()
    {
        using var rig = new Rig();
        using var database = rig.Database();
        var reader = await database.ExecuteReaderAsync("SELECT 42");
        var waiting = rig.Source.AcquireAsync().AsTask();
        Assert.That(waiting.IsCompleted, Is.False);
        reader.Close();
        await reader.DisposeAsync();
        reader.Dispose();
        await using var lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        rig.Readers[0].Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        rig.Commands[0].Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        Assert.That(rig.Opens, Is.EqualTo(1));
    }

    [TestCase("create")]
    [TestCase("provision")]
    [TestCase("execute")]
    [TestCase("read")]
    [TestCase("map")]
    public async Task FailedQuery_ReleasesLeaseForNextQuery(string failure)
    {
        using var rig = new Rig();
        using var database = rig.Database();
        rig.FailCreate = failure == "create";
        rig.FailProvision = failure == "provision";
        rig.FailExecute = failure == "execute";
        rig.FailRead = failure == "read";
        rig.FailMap = failure == "map";
        Assert.Throws<InvalidOperationException>(() => database.ReadMultiple("SELECT 42").ToList());
        foreach (var reader in rig.Readers) reader.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        foreach (var command in rig.Commands) command.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        rig.FailCreate = rig.FailProvision = rig.FailExecute = rig.FailRead = rig.FailMap = false;
        await using var lease = await rig.Source.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void SingleResult_PreservesCardinalityAndReleasesLease(int rows)
    {
        using var rig = new Rig { Rows = rows };
        using var database = rig.Database();
        if (rows == 2) Assert.Throws<InvalidOperationException>(() => database.ReadSingle("SELECT 42"));
        else Assert.That(database.ReadSingle("SELECT 42") is null, Is.EqualTo(rows == 0));
        using var lease = rig.Source.Acquire();
    }

    [Test]
    public async Task MicroOrm_SyncAndAsyncStreamingReleaseLeasesOnEarlyExit()
    {
        using var rig = new Rig();
        using var database = rig.MicroOrm();
        foreach (var _ in database.ReadMultiple<Row>("SELECT 42")) break;
        await foreach (var _ in database.ReadMultipleAsync<Row>("SELECT 42")) break;
        Assert.That(rig.Opens, Is.EqualTo(1));
        await using var lease = await rig.Source.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        rig.Readers[0].Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        rig.Readers[1].Verify(r => r.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task OwnedSource_DisposalWaitsForActiveReader()
    {
        var reader = new Mock<DbDataReader>();
        var command = new Mock<DbCommand>();
        command.SetupGet(c => c.CommandText).Returns("SELECT 1");
        command.Protected().Setup<DbDataReader>("ExecuteDbDataReader", ItExpr.IsAny<CommandBehavior>()).Returns(reader.Object);
        var connection = new Mock<DbConnection>();
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var url = new Mock<ConnectionUrl>("test://localhost");
        url.Setup(c => c.Open()).Returns(connection.Object);
        var provisioners = new Mock<CommandProvisionerFactory>();
        provisioners.Setup(p => p.Instantiate(It.IsAny<ICommandProvider>(), It.IsAny<ConnectionUrl>())).Returns([]);
        var database = new DatabaseUrl(url.Object, provisioners.Object, NullQueryLogger.Instance);
        var ownedReader = database.ExecuteReader("SELECT 1");
        var disposal = database.DisposeAsync().AsTask();
        Assert.That(disposal.IsCompleted, Is.False);
        ownedReader.Dispose();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        Assert.Throws<ObjectDisposedException>(() => database.ExecuteReader("SELECT 1"));
    }
}
