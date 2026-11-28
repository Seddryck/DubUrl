using System.Data.Common;
using DubUrl.Mapping;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class ConnectionUrlScriptTest
{
    [TestCase("mssql", true)]
    [TestCase("sqlserver", true)]
    [TestCase("odbc+mssql", true)]
    [TestCase("mssql+odbc", true)]
    [TestCase("tls+mssql", true)]
    [TestCase("pgsql", false)]
    [TestCase("mysql", false)]
    [TestCase("adomd+ssas", false)]
    public void CanExecuteScript_UsesRegisteredSqlServerRoutes(string scheme, bool expected)
    {
        var registry = new Mock<ISchemeRegistry>(MockBehavior.Strict);
        if (expected)
            registry.Setup(x => x.CanHandle(scheme)).Returns(true);

        var url = new ConnectionUrl($"{scheme}://localhost/db", registry.Object);

        Assert.That(url.CanExecuteScript, Is.EqualTo(expected));
    }

    [Test]
    public void CanExecuteScript_UnregisteredSqlServerRoute_ReturnsFalse()
    {
        var url = new ConnectionUrl("mssql://localhost/db", Mock.Of<ISchemeRegistry>());
        Assert.That(url.CanExecuteScript, Is.False);
    }

    [Test]
    public async Task ExecuteScriptAsync_SplitsBatchesAndUsesOneConnection()
    {
        const string script = "SELECT 'one''\r\nGO\r\ntwo', [three]]\r\nGO\r\nfour], \"five\r\nGO\r\nsix\";\r\n"
            + "/* outer\r\n/* inner */\r\nGO\r\n*/\r\ngo 2 -- repeat\r\n"
            + "SELECT 2;\r\nGO /* carries into\r\nGO\r\n the next batch */\r\nSELECT 3;";
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var (url, connection, command) = SetupSqlServer(token);
        var executed = new List<string>();
        command.Setup(x => x.ExecuteNonQueryAsync(token))
            .Callback(() => executed.Add(command.Object.CommandText))
            .ReturnsAsync(0);

        await url.Object.ExecuteScriptAsync(script, token);

        var firstBatch = script[..script.IndexOf("go 2", StringComparison.Ordinal)];
        Assert.That(executed, Is.EqualTo(new[] { firstBatch, firstBatch,
            " -- repeat\r\nSELECT 2;\r\n", " /* carries into\r\nGO\r\n the next batch */\r\nSELECT 3;" }));
        url.Verify(x => x.OpenAsync(token), Times.Once);
        connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task ExecuteScriptAsync_UnsupportedRoute_ThrowsBeforeOpening()
    {
        var url = new Mock<ConnectionUrl>("pgsql://localhost/db", Mock.Of<ISchemeRegistry>()) { CallBase = true };
        await Assert.ThrowsAsync<NotSupportedException>(() => url.Object.ExecuteScriptAsync("SELECT 1;"));
        url.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestCase("SELECT 1;\nGO 0", typeof(FormatException))]
    [TestCase("SELECT 1;\nGO 2147483648", typeof(FormatException))]
    [TestCase("SELECT 1;\nGO 2 unexpected", typeof(FormatException))]
    public async Task ExecuteScriptAsync_InvalidSeparator_ThrowsBeforeOpening(string script, Type exceptionType)
    {
        var (url, _, _) = SetupSqlServer();
        await Assert.ThrowsAsync(exceptionType, () => url.Object.ExecuteScriptAsync(script));
        url.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteScriptAsync_EmptyBatches_DoNotOpenConnection()
    {
        var (url, _, _) = SetupSqlServer();
        await url.Object.ExecuteScriptAsync("-- comment only\nGO\n/* nothing to execute */\nGO");
        url.Verify(x => x.OpenAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteScriptAsync_StopsAfterCommandFails()
    {
        var (url, connection, command) = SetupSqlServer();
        var error = new InvalidOperationException("Failed to execute.");
        command.Setup(x => x.ExecuteNonQueryAsync(CancellationToken.None)).ThrowsAsync(error);

        Assert.That(await Assert.ThrowsAsync<InvalidOperationException>(() =>
            url.Object.ExecuteScriptAsync("SELECT 1;\nGO 2\nSELECT 2;")), Is.SameAs(error));
        command.Verify(x => x.ExecuteNonQueryAsync(CancellationToken.None), Times.Once);
        connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    [Test]
    public async Task ExecuteScriptAsync_StopsWhenCancelledBetweenRepetitions()
    {
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;
        var (url, connection, command) = SetupSqlServer(token);
        command.Setup(x => x.ExecuteNonQueryAsync(token)).Callback(cancellation.Cancel).ReturnsAsync(0);

        await Assert.CatchAsync<OperationCanceledException>(() =>
            url.Object.ExecuteScriptAsync("SELECT 1;\nGO 2\nSELECT 2;", token));
        command.Verify(x => x.ExecuteNonQueryAsync(token), Times.Once);
        connection.Verify(x => x.DisposeAsync(), Times.Once);
    }

    private static (Mock<ConnectionUrl> Url, Mock<DbConnection> Connection, Mock<DbCommand> Command)
        SetupSqlServer(CancellationToken token = default)
    {
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.CanHandle("mssql")).Returns(true);
        var url = new Mock<ConnectionUrl>("mssql://localhost/db", registry.Object) { CallBase = true };
        var connection = new Mock<DbConnection>();
        var command = new Mock<DbCommand>();
        command.SetupProperty(x => x.CommandText);
        connection.Protected().Setup<DbCommand>("CreateDbCommand").Returns(command.Object);
        url.Setup(x => x.OpenAsync(token)).ReturnsAsync(connection.Object);
        return (url, connection, command);
    }
}
