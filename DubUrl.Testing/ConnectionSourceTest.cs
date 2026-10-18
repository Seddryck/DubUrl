using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace DubUrl.Testing;

[TestFixture]
public class ConnectionSourceTest
{
    internal sealed class TestConnection : DbConnection
    {
        private ConnectionState _state;
        public int Opens { get; private set; }
        public int Disposals { get; private set; }
        public bool FailOpen { get; set; }
        public bool FailDispose { get; set; }
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "test";
        public override string DataSource => "test";
        public override string ServerVersion => "1";
        public override ConnectionState State => _state;
        public void Break() => _state = ConnectionState.Broken;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open()
        {
            Opens++;
            if (FailOpen) throw new InvalidOperationException("Open failed.");
            _state = ConnectionState.Open;
        }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (!disposing) return;
            Disposals++;
            Close();
            if (FailDispose) throw new InvalidOperationException("Dispose failed.");
        }
    }

    internal static ConnectionSourceUrl CreateUrl(Func<DbConnection> create, bool nativeDataSource = true)
    {
        var factory = new Mock<DbProviderFactory> { CallBase = true };
        factory.Setup(x => x.CreateConnection()).Returns(create);
        if (!nativeDataSource)
            factory.Setup(x => x.CreateDataSource(It.IsAny<string>())).Throws<NotSupportedException>();
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(new ResolvedConnection(
            "", Mock.Of<IDialect>(), Mock.Of<IConnectivity>(), Mock.Of<IParametrizer>(), factory.Object));
        return new ConnectionSourceUrl("test://localhost", registry.Object);
    }

    [TestCase(ConnectionReuseMode.ProviderManaged)]
    [TestCase(ConnectionReuseMode.RetainedExclusive)]
    public async Task SequentialLeases_ApplySelectedLifetime(ConnectionReuseMode mode)
    {
        var connections = new List<TestConnection>();
        var url = CreateUrl(() => { var c = new TestConnection(); connections.Add(c); return c; });
        var source = url.Create(new() { ReuseMode = mode });
        Assert.That(connections, Is.Empty);
        DbConnection first;
        using (var lease = source.Acquire()) first = lease.Connection;
        using (var lease = source.Acquire())
            Assert.That(ReferenceEquals(first, lease.Connection), Is.EqualTo(mode == ConnectionReuseMode.RetainedExclusive));
        Assert.That(connections, Has.Count.EqualTo(mode == ConnectionReuseMode.RetainedExclusive ? 1 : 2));
        Assert.That(connections[0].Disposals, Is.EqualTo(mode == ConnectionReuseMode.RetainedExclusive ? 0 : 1));
        await source.DisposeAsync();
        source.Dispose();
        Assert.That(connections.All(c => c.Opens == 1 && c.Disposals == 1), Is.True);
        Assert.Throws<ObjectDisposedException>(() => source.Acquire());
    }

    [Test]
    public async Task ProviderManaged_OverlappingLeasesHaveIndependentConnections()
    {
        await using var source = CreateUrl(() => new TestConnection()).Create(new() { ReuseMode = ConnectionReuseMode.ProviderManaged });
        await using var first = await source.AcquireAsync();
        await using var second = await source.AcquireAsync();
        Assert.That(second.Connection, Is.Not.SameAs(first.Connection));
    }

    [Test]
    public async Task Retained_WaitCancellationDoesNotReleaseActiveLease()
    {
        await using var source = CreateUrl(() => new TestConnection()).Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        var first = await source.AcquireAsync();
        using var cancellation = new CancellationTokenSource();
        var canceled = source.AcquireAsync(cancellation.Token).AsTask();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await canceled);
        var next = source.AcquireAsync().AsTask();
        Assert.That(next.IsCompleted, Is.False);
        first.Dispose();
        await first.DisposeAsync();
        await using var second = await next.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(second.Connection, Is.SameAs(first.Connection));
    }

    [TestCase(ConnectionReuseMode.ProviderManaged)]
    [TestCase(ConnectionReuseMode.RetainedExclusive)]
    public async Task Disposal_WaitsForActiveLeaseAndRejectsNewAcquisitions(ConnectionReuseMode mode)
    {
        var connection = new TestConnection();
        var source = CreateUrl(() => connection).Create(new() { ReuseMode = mode });
        var lease = await source.AcquireAsync();
        var waiter = mode == ConnectionReuseMode.RetainedExclusive ? source.AcquireAsync().AsTask() : null;
        var disposal = source.DisposeAsync().AsTask();
        Assert.That(disposal.IsCompleted, Is.False);
        Assert.That(connection.Disposals, Is.Zero);
        Assert.Throws<ObjectDisposedException>(() => source.Acquire());
        if (waiter is not null)
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => await waiter.WaitAsync(TimeSpan.FromSeconds(5)));
        await lease.DisposeAsync();
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(connection.Disposals, Is.EqualTo(1));
    }

    [TestCase(ConnectionReuseMode.ProviderManaged)]
    [TestCase(ConnectionReuseMode.RetainedExclusive)]
    public async Task FailedOpen_DisposesPartialConnectionAndAllowsLaterAcquire(ConnectionReuseMode mode)
    {
        var failed = new TestConnection { FailOpen = true };
        var healthy = new TestConnection();
        var pending = new Queue<TestConnection>([failed, healthy]);
        await using var source = CreateUrl(() => pending.Dequeue(), nativeDataSource: false).Create(new() { ReuseMode = mode });
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await source.AcquireAsync());
        Assert.That(failed.Disposals, Is.EqualTo(1));
        await using var lease = await source.AcquireAsync();
        Assert.That(lease.Connection, Is.SameAs(healthy));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Retained_ClosedOrBrokenConnectionIsReplaced(bool broken)
    {
        var first = new TestConnection();
        var second = new TestConnection();
        var pending = new Queue<TestConnection>([first, second]);
        await using var source = CreateUrl(() => pending.Dequeue()).Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        using (source.Acquire()) { }
        if (broken) first.Break(); else first.Close();
        await using var lease = await source.AcquireAsync();
        Assert.That(lease.Connection, Is.SameAs(second));
        Assert.That(first.Disposals, Is.EqualTo(1));
    }

    [Test]
    public async Task ProviderManaged_ReleaseFailureStillAllowsSourceDisposal()
    {
        var source = CreateUrl(() => new TestConnection { FailDispose = true }).Create(new() { ReuseMode = ConnectionReuseMode.ProviderManaged });
        var lease = source.Acquire();
        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        await source.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
}
