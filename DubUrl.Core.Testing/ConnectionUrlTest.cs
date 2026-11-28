using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class ConnectionUrlTest
{
    [Test]
    public void CreateBatch_SupportedConnection_OpensAndReturnsProviderBatch()
    {
        var connection = new Mock<DbConnection>(MockBehavior.Strict);
        var batch = new Mock<DbBatch>().Object;
        connection.SetupGet(x => x.CanCreateBatch).Returns(true);
        connection.Setup(x => x.Open());
        connection.Protected().Setup<DbBatch>("CreateDbBatch").Returns(() =>
        {
            connection.Verify(x => x.Open(), Times.Once);
            return batch;
        });

        var result = BatchConnectionUrl(connection.Object).CreateBatch();

        Assert.That(result, Is.SameAs(batch));
    }

    [Test]
    public void CreateBatch_UnsupportedConnection_DisposesWithoutOpening()
    {
        var connection = new Mock<DbConnection>();
        var url = BatchConnectionUrl(connection.Object);

        Assert.Throws<NotSupportedException>(() => url.CreateBatch());

        connection.Verify(x => x.Open(), Times.Never);
        connection.Protected().Verify("CreateDbBatch", Times.Never());
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }

    [Test]
    public void CreateBatch_ConnectionWithoutDbBatchApi_DisposesWithoutOpening()
    {
        var connection = new Mock<IDbConnection>();
        var url = BatchConnectionUrl(connection.Object);

        Assert.Throws<NotSupportedException>(() => url.CreateBatch());

        connection.Verify(x => x.Open(), Times.Never);
        connection.Verify(x => x.Dispose(), Times.Once);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void CreateBatch_ProviderFailure_DisposesConnectionAndPreservesError(bool failOnOpen)
    {
        var failure = new InvalidOperationException("Provider failure");
        var connection = new Mock<DbConnection>();
        connection.SetupGet(x => x.CanCreateBatch).Returns(true);
        if (failOnOpen)
            connection.Setup(x => x.Open()).Throws(failure);
        else
            connection.Protected().Setup<DbBatch>("CreateDbBatch").Throws(failure);
        var url = BatchConnectionUrl(connection.Object);

        Assert.That(Assert.Throws<InvalidOperationException>(() => url.CreateBatch()), Is.SameAs(failure));

        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }

    private static ConnectionUrl BatchConnectionUrl(IDbConnection connection)
    {
        var url = new Mock<ConnectionUrl>("mssql://localhost/db");
        url.Setup(x => x.Connect()).Returns(connection);
        return url.Object;
    }

    private static ResolvedConnection Resolved(string connectionString = "", DbProviderFactory? providerFactory = null)
        => new(
            connectionString,
            Mock.Of<IDialect>(),
            Mock.Of<IConnectivity>(),
            Mock.Of<IParametrizer>(),
            providerFactory ?? Mock.Of<DbProviderFactory>()
        );

    [Test]
    public void Parse_AnyConnectionString_OneCallToParserParse()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri());

        var schemeRegistryMock = new Mock<ISchemeRegistry>();
        schemeRegistryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeRegistryMock.Object);
        connectionUrl.Parse();

        parserMock.Verify(x => x.Parse(url), Times.Once());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToRegistryResolve()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri() { Scheme = new SchemeKey(["mssql"]) });

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Parse();

        registryMock.Verify(x => x.Resolve(It.Is<ConnectionUri>(x => x.Scheme.Parts.Count == 1 && x.Scheme.Parts[0] == "mssql")), Times.Once());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToRegistryResolveResult()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri());

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Parse();

        registryMock.Verify(x => x.Resolve(It.IsAny<ConnectionUri>()), Times.Once());
    }

    [Test]
    public void Connect_AnyConnectionString_OneCallToBuilderMethods()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri());

        var dbConnectionMock = new Mock<DbConnection>();

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved(providerFactory: dbProviderfactoryMock.Object));

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Connect();

        registryMock.VerifyAll();
    }

    [Test]
    public void Connect_AnyConnectionString_CreateWithConnectionStringAlreadySet()
    {
        var url = "mssql://localhost/db";
        var connString = "Data Source=localhost;Initial Catalog=db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri());

        var sequence = new MockSequence();
        var dbConnectionMock = new Mock<DbConnection>();
        dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString=It.IsAny<string>());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Connect();

        dbConnectionMock.VerifySet(x => x.ConnectionString = connString);
        dbConnectionMock.VerifyAll();
        dbConnectionMock.Verify(x => x.Open(), Times.Never());
    }

    [Test]
    public void Open_AnyConnectionString_OpenWithConnectionStringAlreadySet()
    {
        var url = "mssql://localhost/db";
        var connString = "Data Source=localhost;Initial Catalog=db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new ConnectionUri());

        var sequence = new MockSequence();
        var dbConnectionMock = new Mock<DbConnection>();
        dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString = It.IsAny<string>());
        dbConnectionMock.InSequence(sequence).Setup(x => x.Open());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Open();

        dbConnectionMock.VerifySet(x => x.ConnectionString = connString);
        dbConnectionMock.Verify(x => x.Open(), Times.Once());
    }
}
