using DubUrl.Mapping;
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
using DubUrl.DriverLocating;
using Moq;

namespace DubUrl.Testing.Mapping
{
    public class OdbcMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.Odbc";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host", "host", "db", 1234)]
        public void Map_UrlInfo_ReturnsServer(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Server"));
            Assert.That(result["Server"], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_ReturnsInitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Database"));
            Assert.That(result["Database"], Is.EqualTo(expected));
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Uid"));
            Assert.That(result["Uid"], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey("Pwd"));
            Assert.That(result["Pwd"], Is.EqualTo("pwd"));
        }

        [Test]
        public void Map_UrlInfoContainsOptions_OptionsReturned()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
            urlInfo.Options.Add("sslmode", "required");
            urlInfo.Options.Add("charset", "UTF8");

            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("sslmode"));
            Assert.That(result["sslmode"], Is.EqualTo("required"));
            Assert.That(result, Does.ContainKey("charset"));
            Assert.That(result["charset"], Is.EqualTo("UTF8"));
        }

        [Test]
        [TestCase("{MySQL ODBC 5.1 Driver}")]
        [TestCase("MySQL ODBC 5.1 Driver")]
        public void Map_DriverSpecified_DriverAssigned(string driver)
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Driver", driver);

            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Driver"));
            Assert.That(result["Driver"], Is.EqualTo("{MySQL ODBC 5.1 Driver}"));
        }

        [Test]
        public void Map_DriverSpecified_NoDriverLocationCalled()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };

            var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
            driverLocationFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

            var mapper = new OdbcMapper(ConnectionStringBuilder, driverLocationFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            driverLocationFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Map_NoDriverSpecified_DriverLocationCalled()
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "mssql" }, Segments = new[] { "db" } };
            
            var driverLocationMock = new Mock<IDriverLocator>();
            driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
            var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
            driverLocationFactoryMock.Setup(x => 
                    x.Instantiate(It.IsAny<string>(), It.IsAny<IDictionary<Type, object>>())
                ).Returns(driverLocationMock.Object);

            var mapper = new OdbcMapper(ConnectionStringBuilder, driverLocationFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            driverLocationFactoryMock.Verify(x => x.Instantiate("mssql", It.IsAny<IDictionary<Type, object>>()), Times.Once);
            driverLocationMock.Verify(x => x.Locate());
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Driver"));
            Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
        }

        [Test, Pairwise]
        public void Map_NoDriverSpecifiedButOptions_DriverLocationCalled(
                [Values(ArchitectureOption.x64, ArchitectureOption.x86)] ArchitectureOption architecture,
                [Values(EncodingOption.ANSI, EncodingOption.Unicode)] EncodingOption encoding
            )
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "mssql" }, Segments = new[] { "db" }, 
                Options = new Dictionary<string, string>() { 
                    { "Driver-Architecture", Enum.GetName(typeof(ArchitectureOption), architecture) ?? throw new ArgumentNullException()}, 
                    { "Driver-Encoding", Enum.GetName(typeof(EncodingOption), encoding) ?? throw new ArgumentNullException() } 
                }
            };

            var driverLocationMock = new Mock<IDriverLocator>();
            driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
            var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
            driverLocationFactoryMock.Setup(x =>
                    x.Instantiate(It.IsAny<string>(), It.IsAny<IDictionary<Type, object>>())
                ).Returns(driverLocationMock.Object);

            var mapper = new OdbcMapper(ConnectionStringBuilder, driverLocationFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            driverLocationFactoryMock.Verify(
                x => x.Instantiate("mssql", It.Is<IDictionary<Type, object>>(
                    x => x.ContainsKey(typeof(ArchitectureOption))
                        && (ArchitectureOption)x[typeof(ArchitectureOption)] == architecture
                        && x.ContainsKey(typeof(EncodingOption))
                        && (EncodingOption)x[typeof(EncodingOption)] == encoding
                )), Times.Once);
            driverLocationMock.Verify(x => x.Locate());
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Driver"));
            Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
        }
    }
}
