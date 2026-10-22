using DubUrl.Querying;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Data;
using System.Data.Common;

namespace DubUrl.Core.Testing;

public class ConnectionLifetimeRaceTest
{
    private sealed class ConnectionFactory(DbConnection connection) : DbProviderFactory
    {
        public override DbConnection CreateConnection() => connection;
    }

    private sealed class NativeSourceFactory(DbDataSource source) : DbProviderFactory
    {
        public override DbDataSource CreateDataSource(string connectionString) => source;
        public override DbConnection CreateConnection() => throw new InvalidOperationException("The native data source must be used.");
    }

    [Test]
    public void ProviderNativeDataSource_IsPreferredOverFactoryFallback()
    {
        var connection = new ConnectionSourceTest.TestConnection();
        connection.Open();
        var providerSource = new Mock<DbDataSource>();
        providerSource.Protected().Setup<DbConnection>("OpenDbConnection").Returns(connection);
        using var source = ConnectionReusePolicyTest.CreateSource(new NativeSourceFactory(providerSource.Object), "");
        using var lease = source.Acquire();
        Assert.That(lease.Connection, Is.SameAs(connection));
        providerSource.Protected().Verify("OpenDbConnection", Times.Once());
    }

    [Test]
    public void FactoryWithoutNativeDataSource_ConfigurationFailureDisposesConnection()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupSet(c => c.ConnectionString = It.IsAny<string>()).Throws(new ArgumentException("Rejected configuration."));
        using var source = ConnectionReusePolicyTest.CreateSource(new ConnectionFactory(connection.Object), "");
        Assert.Throws<ArgumentException>(() => source.Acquire());
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
    }

    [Test]
    public async Task ContendingRetainedAcquisitions_NeverLendConnectionToTwoOperations()
    {
        var connections = new List<ConnectionSourceTest.TestConnection>();
        await using var source = ConnectionSourceTest.CreateUrl(() =>
        {
            var connection = new ConnectionSourceTest.TestConnection();
            connections.Add(connection);
            return connection;
        }).Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        int active = 0;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workers = Enumerable.Range(0, 16).Select(async _ =>
        {
            await start.Task;
            for (var i = 0; i < 20; i++)
            {
                await using var lease = await source.AcquireAsync();
                Assert.That(Interlocked.Increment(ref active), Is.EqualTo(1));
                try { await Task.Yield(); }
                finally { Interlocked.Decrement(ref active); }
            }
        }).ToArray();
        start.SetResult();
        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(connections, Has.Count.EqualTo(1));
        Assert.That(connections[0].Opens, Is.EqualTo(1));
        Assert.That(connections[0].Disposals, Is.Zero);
    }

    [Test]
    public async Task CancellationOfOneQueuedBorrower_DoesNotDisturbOthers()
    {
        await using var source = ConnectionSourceTest.CreateUrl(() => new ConnectionSourceTest.TestConnection())
            .Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        var active = source.Acquire();
        var connection = active.Connection;
        using var canceled = new CancellationTokenSource();
        var first = source.AcquireAsync(canceled.Token).AsTask();
        var second = source.AcquireAsync().AsTask();
        canceled.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await first);
        Assert.That(second.IsCompleted, Is.False);
        active.Dispose();
        await using var next = await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(next.Connection, Is.SameAs(connection));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SourceDisposal_DisposesNativeDataSourceWithMatchingApi(bool asynchronous)
    {
        var providerSource = new Mock<DbDataSource>();
        providerSource.Protected().Setup<ValueTask>("DisposeAsyncCore").Returns(ValueTask.CompletedTask);
        var source = new ConnectionSource(new ConnectionUrl("test://localhost"), ConnectionReuseMode.ProviderManaged, providerSource.Object);
        if (asynchronous) await source.DisposeAsync();
        else source.Dispose();
        providerSource.Protected().Verify("DisposeAsyncCore", asynchronous ? Times.Once() : Times.Never());
        providerSource.Protected().Verify("Dispose", asynchronous ? Times.Never() : Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }

    [Test]
    public async Task ConnectionAndDataSourceCleanupFailures_AreBothReportedWithoutStrandingDisposal()
    {
        var connection = new Mock<DbConnection>();
        connection.Setup(c => c.DisposeAsync()).Throws(new InvalidOperationException("Connection cleanup failed."));
        var providerSource = new Mock<DbDataSource>();
        providerSource.Protected().Setup<DbConnection>("OpenDbConnection").Returns(connection.Object);
        providerSource.Protected().Setup<ValueTask>("DisposeAsyncCore").Throws(new NotSupportedException("Data source cleanup failed."));
        var source = new ConnectionSource(new ConnectionUrl("test://localhost"), ConnectionReuseMode.RetainedExclusive, providerSource.Object);
        using (source.Acquire()) { }
        var failure = await Assert.ThrowsAsync<AggregateException>(async () => await source.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.That(failure!.InnerExceptions, Has.Count.EqualTo(2));
        providerSource.Protected().Verify("DisposeAsyncCore", Times.Once());
    }

    [Test]
    public async Task CancellationAfterRetainedGateAcquisition_PreservesExistingConnection()
    {
        using var cancellation = new CancellationTokenSource();
        bool cancelOnStateRead = false;
        var connection = new Mock<DbConnection>();
        connection.SetupGet(c => c.State).Returns(() =>
        {
            if (cancelOnStateRead) cancellation.Cancel();
            return ConnectionState.Open;
        });
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        await using var source = ConnectionSourceTest.CreateUrl(() => connection.Object)
            .Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        using (source.Acquire()) { }
        cancelOnStateRead = true;

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await source.AcquireAsync(cancellation.Token));
        connection.Verify(c => c.DisposeAsync(), Times.Never);
        connection.Protected().Verify("Dispose", Times.Never(), ItExpr.IsAny<bool>());
        cancelOnStateRead = false;
        await using var lease = await source.AcquireAsync();
        Assert.That(lease.Connection, Is.SameAs(connection.Object));
        connection.Verify(c => c.Open(), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RepeatedLeaseDisposal_WaitsForSameCleanup(bool fail)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Mock<DbConnection>();
        connection.Setup(c => c.DisposeAsync()).Returns(() => new ValueTask(finish.Task));
        await using var source = ConnectionSourceTest.CreateUrl(() => connection.Object)
            .Create(new() { ReuseMode = ConnectionReuseMode.ProviderManaged });
        var lease = source.Acquire();
        var first = lease.DisposeAsync().AsTask();
        var second = lease.DisposeAsync().AsTask();
        try { Assert.That(second.IsCompleted, Is.False, "A repeated disposal must wait for cleanup to finish."); }
        finally
        {
            if (fail) finish.SetException(new InvalidOperationException("Cleanup failed."));
            else finish.SetResult();
            if (fail)
            {
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await second);
            }
            else await Task.WhenAll(first, second);
        }
        connection.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RepeatedReaderDisposal_WaitsForReaderCleanupBeforeReturningLease(bool fail)
    {
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var inner = new Mock<DbDataReader>();
        inner.Setup(r => r.DisposeAsync()).Returns(() => new ValueTask(finish.Task));
        var command = new Mock<IDbCommand>();
        await using var source = ConnectionSourceTest.CreateUrl(() => new ConnectionSourceTest.TestConnection())
            .Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        var lease = source.Acquire();
        var reader = new OwnedDataReader(inner.Object, command.Object, lease);
        var first = reader.DisposeAsync().AsTask();
        var second = reader.DisposeAsync().AsTask();
        var next = source.AcquireAsync().AsTask();
        try
        {
            Assert.That(second.IsCompleted, Is.False);
            Assert.That(next.IsCompleted, Is.False);
        }
        finally
        {
            if (fail) finish.SetException(new InvalidOperationException("Reader cleanup failed."));
            else finish.SetResult();
            try
            {
                if (fail)
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(async () => await first);
                    await Assert.ThrowsAsync<InvalidOperationException>(async () => await second);
                }
                else await Task.WhenAll(first, second);
            }
            finally
            {
                await using var acquired = await next.WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        inner.Verify(r => r.DisposeAsync(), Times.Once);
        command.Verify(c => c.Dispose(), Times.Once);
    }

    [Test]
    public async Task Shutdown_ThrowingProviderCancellationCallbackStillFinishesCleanup()
    {
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Mock<DbConnection>();
        connection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>())).Returns(async (CancellationToken token) =>
        {
            using var callback = token.Register(() => throw new InvalidOperationException("Provider cancellation callback failed."));
            var wait = Task.Delay(Timeout.Infinite, token);
            opening.SetResult();
            await wait.ConfigureAwait(false);
        });
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var source = ConnectionSourceTest.CreateUrl(() => connection.Object, nativeDataSource: false)
            .Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        var acquisition = source.AcquireAsync().AsTask();
        await opening.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Task? disposal = null;
        Assert.DoesNotThrow(() => disposal = source.DisposeAsync().AsTask());
        await Assert.ThrowsAsync<AggregateException>(async () => await disposal!.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await acquisition.WaitAsync(TimeSpan.FromSeconds(5)));
        await Assert.ThrowsAsync<AggregateException>(async () => await source.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        connection.Verify(c => c.DisposeAsync(), Times.Once);
    }

    [Test]
    public void SynchronousSourceDisposal_UsesSynchronousProviderCleanup()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupGet(c => c.State).Returns(ConnectionState.Open);
        connection.Setup(c => c.DisposeAsync()).Throws(new NotSupportedException("Only synchronous disposal is supported."));
        var source = ConnectionSourceTest.CreateUrl(() => connection.Object)
            .Create(new() { ReuseMode = ConnectionReuseMode.RetainedExclusive });
        using (source.Acquire()) { }
        Assert.DoesNotThrow(() => source.Dispose());
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
        connection.Verify(c => c.DisposeAsync(), Times.Never);
    }

    [Test]
    public void ConnectionConfigurationFailure_DisposesPartiallyCreatedConnection()
    {
        var connection = new Mock<DbConnection>();
        connection.SetupSet(c => c.ConnectionString = It.IsAny<string>()).Throws(new ArgumentException("Rejected configuration."));
        using var source = ConnectionSourceTest.CreateUrl(() => connection.Object, nativeDataSource: false).Create();
        Assert.Throws<ArgumentException>(() => source.Acquire());
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.IsAny<bool>());
    }

    [Test]
    public void OwningCommand_CannotExecuteAgainAfterReaderTakesItsLease()
    {
        var inner = new Mock<IDbCommand>();
        var reader = new Mock<IDataReader>();
        inner.Setup(c => c.ExecuteReader(It.IsAny<CommandBehavior>())).Returns(reader.Object);
        var owner = new Mock<IDisposable>();
        using var command = new OwnedDbCommand(inner.Object, Mock.Of<IDbConnection>(), owner.Object);
        using var result = command.ExecuteReader();

        Assert.Throws<InvalidOperationException>(() => command.ExecuteReader());
        Assert.Throws<InvalidOperationException>(() => command.ExecuteScalar());
        Assert.Throws<InvalidOperationException>(() => command.ExecuteNonQuery());
        command.Dispose();
        owner.Verify(o => o.Dispose(), Times.Never);
    }

    [Test]
    public async Task Shutdown_ProviderIgnoringCancellationCannotReturnALeaseAfterDisposal()
    {
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var connection = new Mock<DbConnection>();
        connection.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>())).Returns(async () =>
        {
            opening.SetResult();
            await proceed.Task.ConfigureAwait(false);
        });
        connection.Setup(c => c.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var source = ConnectionSourceTest.CreateUrl(() => connection.Object, nativeDataSource: false).Create();
        var acquisition = source.AcquireAsync().AsTask();
        await opening.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var disposal = source.DisposeAsync().AsTask();
        Assert.That(disposal.IsCompleted, Is.False);
        proceed.SetResult();
        await Assert.ThrowsAsync<ObjectDisposedException>(async () => await acquisition.WaitAsync(TimeSpan.FromSeconds(5)));
        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
        connection.Verify(c => c.DisposeAsync(), Times.Once);
    }
}
