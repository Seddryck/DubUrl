using System;
using System.Collections.Generic;
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

            var mapperFactoryMock = new Mock<MapperFactory>();
            mapperFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>())).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, mapperFactoryMock.Object);
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

            var mapperFactoryMock = new Mock<MapperFactory>();
            mapperFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>())).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, mapperFactoryMock.Object);
            urlConnection.Parse();

            mapperFactoryMock.Verify(x => x.Instantiate("mssql"), Times.Once());
        }

        [Test]
        public void Parse_AnyConnectionString_OneCallToMapperMap()
        {
            var url = "mssql://localhost/db";

            var parserMock = new Mock<IParser>();
            parserMock.Setup(x => x.Parse(It.IsAny<string>())).Returns(new UrlInfo());

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(x => x.Map(It.IsAny<UrlInfo>()));

            var mapperFactoryMock = new Mock<MapperFactory>();
            mapperFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>())).Returns(mapperMock.Object);

            var urlConnection = new UrlConnection(url, parserMock.Object, mapperFactoryMock.Object);
            urlConnection.Parse();

            mapperMock.Verify(x => x.Map(It.IsAny<UrlInfo>()), Times.Once());
        }

    }
}
