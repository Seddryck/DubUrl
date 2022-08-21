using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using System.Data.Odbc;
using DubUrl.Locating.OdbcDriver;
using Moq;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialecting;
using System.IO;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class OdbcDbqMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.Odbc";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
        }

        [Test]
        [TestCase("sheet.xlsx", "")]
        [TestCase("localhost", "sheet.xlsx")]
        [TestCase(".", "sheet.xlsx")]
        [TestCase("", "sheet.xlsx")]
        public void Map_DoubleSlash_Dbq(string host, string segmentsList, string expected = "sheet.xlsx")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.SERVER_KEYWORD));
            Assert.That(result[OdbcDbqMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("localhost", "directory/sheet.xlsx")]
        [TestCase(".", "directory/sheet.xlsx")]
        [TestCase("", "directory/sheet.xlsx")]
        public void Map_TripleSlash_Dbq(string host, string segmentsList, string expected = "directory/sheet.xlsx")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.SERVER_KEYWORD));
            Assert.That(result[OdbcDbqMapper.SERVER_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
        }


        [Test]
        public void Map_QuadrupleSlash_DataSource()
        {
            var path = "c:/directory/sheet.xlsx";
            var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.SERVER_KEYWORD));
            Assert.That(result[OdbcDbqMapper.SERVER_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.USERNAME_KEYWORD));
            Assert.That(result[OdbcDbqMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.PASSWORD_KEYWORD));
            Assert.That(result[OdbcDbqMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }

        [Test]
        public void Map_OptionsContainsOptions_OptionsReturned()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Schemes = new[] { "odbc", "xlsx", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } };
            urlInfo.Options.Add("sslmode", "required");
            urlInfo.Options.Add("charset", "UTF8");

            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("sslmode"));
            Assert.That(result["sslmode"], Is.EqualTo("required"));
            Assert.That(result, Does.ContainKey("charset"));
            Assert.That(result["charset"], Is.EqualTo("UTF8"));
        }

        [Test]
        public void Map_SchemeContainsDriverName_DriverNameReturned()
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "xlsx", "{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}" }, Segments = new[] { "db" } };

            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OdbcDbqMapper.DRIVER_KEYWORD));
            Assert.That(result[OdbcDbqMapper.DRIVER_KEYWORD], Is.EqualTo("{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}"));
        }

        [Test]
        public void Map_DriverSpecified_NoDriverLocationCalled()
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "xlsx", "{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}" }, Segments = new[] { "db" } };

            var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
            driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "odbc+xlsx", "xlsx" });
            driverLocationFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()), driverLocationFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            driverLocationFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Map_NoDriverSpecifiedNoAdditionalOption_DriverLocationCalled()
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "xlsx" }, Segments = new[] { "sheet.xlsx" } };

            var driverLocationMock = new Mock<IDriverLocator>();
            driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
            var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
            driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "xlsx", "odbc+xlsx" });
            driverLocationFactoryMock.Setup(x =>
                    x.Instantiate(It.IsAny<string>())
                ).Returns(driverLocationMock.Object);

            var mapper = new OdbcDbqMapper(ConnectionStringBuilder, new MsExcelDialect(Array.Empty<string>()), driverLocationFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            driverLocationFactoryMock.Verify(x => x.GetValidAliases(), Times.AtLeastOnce);
            driverLocationFactoryMock.Verify(x => x.Instantiate("xlsx"), Times.Once);
            driverLocationMock.Verify(x => x.Locate());
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Driver"));
            Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
        }

    }
}
