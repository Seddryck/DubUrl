using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing
{
    public class ConnectionUrlTest
    {

        [Test]
        public void Parse_AnyConnectionString_OneCallToParserParse()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
            connectionUrl.Parse();

            parserMock.Verify(x => x.Parse(url), Times.Once());
        }

        [Test]
        public void Parse_AnyConnectionString_OneCallToMapperFactoryInstantiate()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo() { Schemes = new[] { "mssql" } });

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
            connectionUrl.Parse();

            schemeMapperBuilderMock.Verify(x => x.Build(), Times.Once());
            schemeMapperBuilderMock.Verify(x => x.GetMapper(It.Is<string[]>(x => x.Length==1 || x.First()=="mssql")), Times.AtLeastOnce());
        }

        [Test]
        public void Parse_AnyConnectionString_OneCallToMapperMap()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
            connectionUrl.Parse();

            mapperMock.Verify(x => x.Rewrite(It.IsAny<UrlInfo>()), Times.Once());
        }

        [Test]
        public void Connect_AnyConnectionString_OneCallToBuilderMethods()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));

            var dbConnectionMock = new Mock<DbConnection>();

            var dbProviderfactoryMock = new Mock<DbProviderFactory>();
            dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
            schemeMapperBuilderMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
            connectionUrl.Connect();

            schemeMapperBuilderMock.VerifyAll();
        }

        [Test]
        public void Connect_AnyConnectionString_CreateWithConnectionStringAlreadySet()
        {
            var url = "mssql://localhost/db";
            var connString = "Data Source=localhost;Initial Catalog=db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));
            mapperMock.Setup(x => x.GetConnectionString()).Returns(connString);

            var sequence = new MockSequence();
            var dbConnectionMock = new Mock<DbConnection>();
            dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString=It.IsAny<string>());

            var dbProviderfactoryMock = new Mock<DbProviderFactory>();
            dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
            schemeMapperBuilderMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
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

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Rewrite(It.IsAny<UrlInfo>()));
            mapperMock.Setup(x => x.GetConnectionString()).Returns(connString);

            var sequence = new MockSequence();
            var dbConnectionMock = new Mock<DbConnection>();
            dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString = It.IsAny<string>());
            dbConnectionMock.InSequence(sequence).Setup(x => x.Open());

            var dbProviderfactoryMock = new Mock<DbProviderFactory>();
            dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build());
            schemeMapperBuilderMock.Setup(x => x.GetMapper(It.IsAny<string[]>())).Returns(mapperMock.Object);
            schemeMapperBuilderMock.Setup(x => x.GetProviderFactory(It.IsAny<string[]>())).Returns(dbProviderfactoryMock.Object);

            var connectionUrl = new ConnectionUrl(url, parserMock.Object, schemeMapperBuilderMock.Object);
            connectionUrl.Open();

            dbConnectionMock.VerifySet(x => x.ConnectionString = connString);
            dbConnectionMock.Verify(x => x.Open(), Times.Once());
        }

    }
}
