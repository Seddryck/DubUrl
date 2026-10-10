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

#if NET7_0_OR_GREATER
public class DataSourceUrlTest
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

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved());

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        dataSourceUrl.Parse();

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

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        dataSourceUrl.Parse();

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

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        dataSourceUrl.Parse();

        registryMock.Verify(x => x.Resolve(It.IsAny<UrlInfo>()), Times.Once());
    }

    [Test]
    public void Create_AnyConnectionUrl_OneCallToBuilderMethods()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(It.IsAny<string>())).Returns(Mock.Of<DbDataSource>());

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(providerFactory: dbProviderfactoryMock.Object));

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        dataSourceUrl.Create();

        registryMock.VerifyAll();
    }

    [Test]
    public void Create_AnyConnectionUrl_CreateWithExpectedConnectionString()
    {
        var url = "mssql://localhost/db";
        var connString = "Data Source=localhost;Initial Catalog=db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(connString)).Returns(Mock.Of<DbDataSource>());

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        dataSourceUrl.Create();

        dbProviderfactoryMock.Verify(x => x.CreateDataSource(connString), Times.Once());
        dbProviderfactoryMock.VerifyAll();
    }

    [Test]
    public void Create_AnyConnectionUrl_DbDataSourceFromDbProviderFactory()
    {
        var url = "mssql://localhost/db";
        var connString = "Data Source=localhost;Initial Catalog=db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var dbDataSource = Mock.Of<DbDataSource>();
        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(connString)).Returns(dbDataSource);

        var registryMock = new Mock<ISchemeRegistry>();
        registryMock.Setup(x => x.Resolve(It.IsAny<UrlInfo>())).Returns(Resolved(connString, dbProviderfactoryMock.Object));

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, registryMock.Object);
        Assert.That(dataSourceUrl.Create(), Is.EqualTo(dbDataSource));
    }
}
#endif
