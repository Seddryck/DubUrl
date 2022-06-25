using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl;
using DubUrl.Mapping;
using DubUrl.Parsing;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing
{
    public class UrlConnectionTest
    {

        [Test]
        public void Parse_AnyConnectionString_OneCallToParserParse()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build(It.IsAny<string>()));
            schemeMapperBuilderMock.Setup(x => x.GetMapper()).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, schemeMapperBuilderMock.Object);
            urlConnection.Parse();

            parserMock.Verify(x => x.Parse(url), Times.Once());
        }

        [Test]
        public void Parse_AnyConnectionString_OneCallToMapperFactoryInstantiate()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo("mssql"));

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build(It.IsAny<string>()));
            schemeMapperBuilderMock.Setup(x => x.GetMapper()).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, schemeMapperBuilderMock.Object);
            urlConnection.Parse();

            schemeMapperBuilderMock.Verify(x => x.Build("mssql"), Times.Once());
            schemeMapperBuilderMock.Verify(x => x.GetMapper(), Times.AtLeastOnce());
        }

        [Test]
        public void Parse_AnyConnectionString_OneCallToMapperMap()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build(It.IsAny<string>()));
            schemeMapperBuilderMock.Setup(x => x.GetMapper()).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, schemeMapperBuilderMock.Object);
            urlConnection.Parse();

            mapperMock.Verify(x => x.Map(It.IsAny<UrlInfo>()), Times.Once());
        }

        [Test]
        public void Open_AnyConnectionString_OneCallToBuilderMethods()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));

            var dbConnectionMock = new Mock<DbConnection>();
            dbConnectionMock.Setup(x => x.Open());

            var dbProviderfactoryMock = new Mock<DbProviderFactory>();
            dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build(It.IsAny<string>()));
            schemeMapperBuilderMock.Setup(x => x.GetMapper()).Returns(mapperMock.Object);
            schemeMapperBuilderMock.Setup(x => x.GetProviderFactory()).Returns(dbProviderfactoryMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, schemeMapperBuilderMock.Object);
            urlConnection.Open();

            schemeMapperBuilderMock.VerifyAll();
        }

        [Test]
        public void Open_AnyConnectionString_OpenWithConnectionStringAlreadySet()
        {
            var url = "mssql://localhost/db";
            var connString = "Data Source=localhost;Initial Catalog=db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));
            mapperMock.Setup(x => x.GetConnectionString()).Returns(connString);

            var sequence = new MockSequence();
            var dbConnectionMock = new Mock<DbConnection>();
            dbConnectionMock.InSequence(sequence).SetupSet(x => x.ConnectionString=It.IsAny<string>());
            dbConnectionMock.InSequence(sequence).Setup(x => x.Open());

            var dbProviderfactoryMock = new Mock<DbProviderFactory>();
            dbProviderfactoryMock.Setup(x => x.CreateConnection()).Returns(dbConnectionMock.Object);

            var schemeMapperBuilderMock = new Mock<SchemeMapperBuilder>();
            schemeMapperBuilderMock.Setup(x => x.Build(It.IsAny<string>()));
            schemeMapperBuilderMock.Setup(x => x.GetMapper()).Returns(mapperMock.Object);
            schemeMapperBuilderMock.Setup(x => x.GetProviderFactory()).Returns(dbProviderfactoryMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, schemeMapperBuilderMock.Object);
            urlConnection.Open();

            dbConnectionMock.VerifySet(x => x.ConnectionString = connString);
            dbConnectionMock.VerifyAll();
        }
    }
}
