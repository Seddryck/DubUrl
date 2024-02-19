using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing;

#if NET7_0_OR_GREATER
public class DataSourceUrlTest
{
    [Test]
    public void Parse_AnyConnectionString_OneCallToParserParse()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
        dataSourceUrl.Parse();

        parserMock.Verify(x => x.Parse(url), Times.Once());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToMapperFactoryInstantiate()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo() { Schemes = ["mssql"] });

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
        dataSourceUrl.Parse();

        schemeMapperMock.Verify(x => x.GetMapper(It.Is<string[]>(x => x.Length == 1 || x.First() == "mssql")), Times.AtLeastOnce());
    }

    [Test]
    public void Parse_AnyConnectionString_OneCallToMapperMap()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
        dataSourceUrl.Parse();

        mapperMock.Verify(x => x.Rewrite(It.IsAny<UrlInfo>()), Times.Once());
    }

    [Test]
    public void Create_AnyConnectionUrl_OneCallToBuilderMethods()
    {
        var url = "mssql://localhost/db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(It.IsAny<string>())).Returns(Mock.Of<DbDataSource>());

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
        schemeMapperMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
        dataSourceUrl.Create();

        schemeMapperMock.VerifyAll();
    }

    [Test]
    public void Create_AnyConnectionUrl_CreateWithExpectedConnectionString()
    {
        var url = "mssql://localhost/db";
        var connString = "Data Source=localhost;Initial Catalog=db";

        var parserMock = new Mock<IParser>();
        parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));
        mapperMock.Setup(x => x.GetConnectionString()).Returns(connString);

        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(connString)).Returns(Mock.Of<DbDataSource>());

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
        schemeMapperMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
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

        var mapperMock = new Mock<IMapper>();
        mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));
        mapperMock.Setup(x => x.GetConnectionString()).Returns(connString);

        var dbDataSource = Mock.Of<DbDataSource>();
        var dbProviderfactoryMock = new Mock<DbProviderFactory>();
        dbProviderfactoryMock.Setup(x => x.CreateDataSource(connString)).Returns(dbDataSource);

        var normalizer = new Mock<SchemeNormalizer>(new Dictionary<string, ISchemeHandler>());

        var schemeMapperMock = new Mock<SchemeMapper>(new Dictionary<string, IMapper> { { "mssql", mapperMock.Object } }, normalizer.Object);
        schemeMapperMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
        schemeMapperMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

        var dataSourceUrl = new DataSourceUrl(url, parserMock.Object, schemeMapperMock.Object);
        Assert.That(dataSourceUrl.Create(), Is.EqualTo(dbDataSource));
    }
}
#endif
