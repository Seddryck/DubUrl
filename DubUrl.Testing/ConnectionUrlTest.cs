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
using NUnit.Framework;

namespace DubUrl.Testing;

public class ConnectionUrlTest
{
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
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var schemeRegistryMock = new Mock<ISchemeRegistry>();
        schemeRegistryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeRegistryMock.Object);
        connectionUrl.Parse();

        parserMock.Verify(x => x.Parse(url), Times.Once());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToRegistryResolve()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo() { Schemes = ["mssql"] });

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Parse();

        registryMock.Verify(x => x.Resolve(It.Is<UrlInfo>(x => x.Schemes.Length == 1 && x.Schemes[0] == "mssql")), Times.Once());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToRegistryResolveResult()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved());

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Parse();

        registryMock.Verify(x => x.Resolve(It.IsAny<UrlInfo>()), Times.Once());
    }

    [Test]
    public void Connect_AnyConnectionString_OneCallToBuilderMethods()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var dbConnectionMock = new Mock<DbConnection>();

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(providerFactory: dbProviderfactoryMock.Object));

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
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var sequence = new MockSequence();
        var dbConnectionMock = new Mock<DbConnection>();
        dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString=It.IsAny<string>());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

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
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var sequence = new MockSequence();
        var dbConnectionMock = new Mock<DbConnection>();
        dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString = It.IsAny<string>());
        dbConnectionMock.InSequence(sequence).Setup(x => x.Open());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

        var connectionUrl = new ConnectionUrl(url, parserMock.Object, registryMock.Object);
        connectionUrl.Open();

        dbConnectionMock.VerifySet(x => x.ConnectionString = connString);
        dbConnectionMock.Verify(x => x.Open(), Times.Once());
    }
}
