using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using Moq.Protected;
using NUnit.Framework;
using System.Data;
using System.Data.Common;

namespace DubUrl.Core.Testing;

public class DatabaseCommandLifecycleTest
{
    [Test]
    public void ReadScalar_Success_DisposesCommandAndConnectionExactlyOnce()
    {
        var command = new Mock<IDbCommand>();
        command.SetupGet(x => x.CommandText).Returns("SELECT 1");
        command.Setup(x => x.ExecuteScalar()).Returns(1);
        var connection = new Mock<IDbConnection>();
        connection.Setup(x => x.CreateCommand()).Returns(command.Object);
        var logger = new Mock<IQueryLogger>();
        var database = CreateDatabase(connection.Object, logger.Object);

        Assert.That(database.ReadScalar("SELECT 1"), Is.EqualTo(1));

        command.Verify(x => x.Dispose(), Times.Once);
        connection.Verify(x => x.Dispose(), Times.Once);
        logger.Verify(x => x.Log("SELECT 1"), Times.Once);
    }

    [Test]
    public void ReadFirst_Success_DisposesReaderCommandAndConnectionExactlyOnce()
    {
        var reader = CreateReader(readResults: [true]);
        var (database, command, connection) = CreateDatabase(reader.Object);

        Assert.That(database.ReadFirst("SELECT 1"), Is.Not.Null);

        reader.Verify(x => x.Dispose(), Times.Once);
        command.Verify(x => x.Dispose(), Times.Once);
        connection.Verify(x => x.Dispose(), Times.Once);
    }

    [Test]
    public void ReadMultiple_Success_DisposesReaderCommandAndConnectionExactlyOnce()
    {
        var reader = CreateReader(readResults: [true, true, false]);
        var (database, command, connection) = CreateDatabase(reader.Object);

        Assert.That(database.ReadMultiple("SELECT 1").ToList(), Has.Count.EqualTo(2));

        reader.Verify(x => x.Dispose(), Times.Once);
        command.Verify(x => x.Dispose(), Times.Once);
        connection.Verify(x => x.Dispose(), Times.Once);
    }

    [Test]
    public void ReadFirst_ReaderThrows_DisposesReaderCommandAndConnectionExactlyOnce()
    {
        var reader = new Mock<IDataReader>();
        reader.Setup(x => x.Read()).Throws<InvalidOperationException>();
        var (database, command, connection) = CreateDatabase(reader.Object);

        Assert.That(() => database.ReadFirst("SELECT 1"), Throws.InvalidOperationException);

        reader.Verify(x => x.Dispose(), Times.Once);
        command.Verify(x => x.Dispose(), Times.Once);
        connection.Verify(x => x.Dispose(), Times.Once);
    }

    [Test]
    public void ExecuteReader_DisposedTwice_DisposesOwnedResourcesExactlyOnce()
    {
        var reader = CreateReader(readResults: [false]);
        var (database, command, connection) = CreateDatabase(reader.Object);

        var ownedReader = database.ExecuteReader("SELECT 1");
        ownedReader.Dispose();
        ownedReader.Dispose();

        reader.Verify(x => x.Dispose(), Times.Once);
        command.Verify(x => x.Dispose(), Times.Once);
        connection.Verify(x => x.Dispose(), Times.Once);
    }

    [Test]
    public async Task ReadFirstAsync_Success_ForwardsCancellationAndDisposesAllResourcesExactlyOnce()
    {
        using var cancellationSource = new CancellationTokenSource();
        var token = cancellationSource.Token;
        var reader = new Mock<DbDataReader>();
        reader.SetupSequence(x => x.ReadAsync(token)).ReturnsAsync(true);
        reader.SetupGet(x => x.FieldCount).Returns(0);
        reader.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var command = new Mock<DbCommand>();
        command.SetupGet(x => x.CommandText).Returns("SELECT 1");
        command.Protected()
            .Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), token)
            .ReturnsAsync(reader.Object);
        command.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.Is<CancellationToken>(t => t.CanBeCanceled))).Returns(Task.CompletedTask);
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        connection.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var connectionUrl = new Mock<ConnectionUrl>(It.IsAny<string>());
        connectionUrl.Setup(x => x.Connect()).Returns(connection.Object);
        var database = new DatabaseUrl(connectionUrl.Object, CreateProvisionerFactory(), NullQueryLogger.Instance);

        Assert.That(await database.ReadFirstAsync("SELECT 1", token), Is.Not.Null);

        // Acquisition links caller cancellation with source shutdown; commands/readers keep the original token.
        connection.Verify(x => x.OpenAsync(It.Is<CancellationToken>(t => t.CanBeCanceled)), Times.Once);
        reader.Verify(x => x.ReadAsync(token), Times.Once);
        reader.Verify(x => x.DisposeAsync(), Times.Once);
        command.Verify(x => x.DisposeAsync(), Times.Once);
        connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task ReadMultipleAsync_EnumerationStopsEarly_DisposesAllResourcesExactlyOnce()
    {
        var reader = new Mock<DbDataReader>();
        reader.Setup(x => x.ReadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        reader.SetupGet(x => x.FieldCount).Returns(0);
        reader.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var command = new Mock<DbCommand>();
        command.SetupGet(x => x.CommandText).Returns("SELECT 1");
        command.Protected()
            .Setup<Task<DbDataReader>>("ExecuteDbDataReaderAsync", ItExpr.IsAny<CommandBehavior>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(reader.Object);
        command.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.OpenAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        connection.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);

        var connectionUrl = new Mock<ConnectionUrl>(It.IsAny<string>());
        connectionUrl.Setup(x => x.Connect()).Returns(connection.Object);
        var database = new DatabaseUrl(connectionUrl.Object, CreateProvisionerFactory(), NullQueryLogger.Instance);

        await foreach (var _ in database.ReadMultipleAsync("SELECT 1"))
            break;

        reader.Verify(x => x.DisposeAsync(), Times.Once);
        command.Verify(x => x.DisposeAsync(), Times.Once);
        connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private static Mock<IDataReader> CreateReader(params bool[] readResults)
    {
        var reader = new Mock<IDataReader>();
        reader.SetupGet(x => x.FieldCount).Returns(0);
        var sequence = reader.SetupSequence(x => x.Read());
        foreach (var result in readResults)
            sequence.Returns(result);
        return reader;
    }

    private static (DatabaseUrl Database, Mock<IDbCommand> Command, Mock<IDbConnection> Connection) CreateDatabase(IDataReader reader)
    {
        var command = new Mock<IDbCommand>();
        command.SetupGet(x => x.CommandText).Returns("SELECT 1");
        command.Setup(x => x.ExecuteReader()).Returns(reader);
        var connection = new Mock<IDbConnection>();
        connection.Setup(x => x.CreateCommand()).Returns(command.Object);
        return (CreateDatabase(connection.Object, NullQueryLogger.Instance), command, connection);
    }

    private static DatabaseUrl CreateDatabase(IDbConnection connection, IQueryLogger logger)
    {
        var connectionUrl = new Mock<ConnectionUrl>(It.IsAny<string>());
        connectionUrl.Setup(x => x.Open()).Returns(connection);
        return new DatabaseUrl(connectionUrl.Object, CreateProvisionerFactory(), logger);
    }

    private static CommandProvisionerFactory CreateProvisionerFactory()
    {
        var factory = new Mock<CommandProvisionerFactory>();
        factory.Setup(x => x.Instantiate(It.IsAny<ICommandProvider>(), It.IsAny<ConnectionUrl>())).Returns([]);
        return factory.Object;
    }
}
