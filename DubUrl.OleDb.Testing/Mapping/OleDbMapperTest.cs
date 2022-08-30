using DubUrl.Mapping;
using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.OleDb;
using Moq;
using System.Runtime.InteropServices;
using DubUrl.Querying.Dialecting;
using DubUrl.Testing.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.OleDb.Providers;
using DubUrl.Mapping.Tokening;

namespace DubUrl.OleDb.Testing.Mapping
{
    [Platform("Win")]
    public class OleDbMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.OleDb";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OleDbFactory.Instance);
                throw new PlatformNotSupportedException("OleDb providers are only accessible on Windows");
            }
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host,1234", "host", "db", 1234)]
        public void Map_UrlInfo_Server(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Ignore("OleDb is Windows only");
            else
            {
                var csb = new OleDbConnectionStringBuilder();
                var specificator = new SpecificatorStraight(csb);
                var mapper = new OleDbMapper.ServerMapper();
                mapper.Accept(specificator);

                var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };
                mapper.Execute(urlInfo);

                Assert.That(csb, Is.Not.Null);
                Assert.That(csb, Does.ContainKey(OleDbMapper.SERVER_KEYWORD));
                Assert.That(csb[OleDbMapper.SERVER_KEYWORD], Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase("MsExcel\\customer.xlsx", "MsExcel/customer.xlsx")]
        [TestCase("customer.xlsx", "customer.xlsx")]
        public void Map_UrlInfo_DataSource(string expected, string segmentsList)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Ignore("OleDb is Windows only");
            else
            {
                var csb = new OleDbConnectionStringBuilder();
                var specificator = new SpecificatorStraight(csb);
                var mapper = new OleDbMapper.DataSourceMapper();
                mapper.Accept(specificator);

                var urlInfo = new UrlInfo() { Host = string.Empty, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };
                mapper.Execute(urlInfo);

                Assert.That(csb, Is.Not.Null);
                Assert.That(csb, Does.ContainKey(OleDbMapper.SERVER_KEYWORD));
                Assert.That(csb[OleDbMapper.SERVER_KEYWORD], Is.EqualTo(expected));
            }
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_ReturnsInitialCatalog(string segmentsList = "db", string expected = "db")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Assert.Ignore("OleDb is Windows only");
            else
            {
                var csb = new OleDbConnectionStringBuilder();
                var specificator = new SpecificatorStraight(csb);
                var mapper = new OleDbMapper.InitialCatalogMapper();
                mapper.Accept(specificator);

                var urlInfo = new UrlInfo() { Host = string.Empty, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };
                mapper.Execute(urlInfo);

                Assert.That(csb, Is.Not.Null);
                Assert.That(csb, Does.ContainKey(OleDbMapper.DATABASE_KEYWORD));
                Assert.That(csb[OleDbMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
            }
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };
            var mapper = new OleDbMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OleDbMapper.USERNAME_KEYWORD));
            Assert.That(result[OleDbMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(OleDbMapper.PASSWORD_KEYWORD));
            Assert.That(result[OleDbMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }

        [Test]
        public void Map_UrlInfoContainsOptions_OptionsReturned()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };
            urlInfo.Options.Add("Persist Security Info", "true");

            var mapper = new OleDbMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Persist Security Info"));
            Assert.That(result["Persist Security Info"], Is.True);
        }

        [Test]
        [TestCase("MSOLAP.11")]
        public void Map_ProviderSpecified_ProviderAssigned(string driver)
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Provider", driver);

            var mapper = new OleDbMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OleDbMapper.PROVIDER_KEYWORD));
            Assert.That(result[OleDbMapper.PROVIDER_KEYWORD], Is.EqualTo("MSOLAP.11"));
        }

        [Test]
        public void Map_ProviderSpecified_NoProviderLocationCalled()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Provider", "OleDb Provider 18 for SQL Server" } } };

            var providerLocatorFactoryMock = new Mock<ProviderLocatorFactory>();
            providerLocatorFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

            var mapper = new OleDbMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()), providerLocatorFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            providerLocatorFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void Map_NoProviderSpecified_ProviderLocationCalled()
        {
            var urlInfo = new UrlInfo() { Schemes = new[] { "oledb", "myprovider" }, Segments = new[] { "db" } };

            var providerLocatorMock = new Mock<IProviderLocator>();
            providerLocatorMock.Setup(x => x.Locate()).Returns("My provider");
            providerLocatorMock.SetupGet(x => x.AdditionalMappers).Returns(new[] { new BaseMapper.OptionsMapper() });
            var providerLocatorFactoryMock = new Mock<ProviderLocatorFactory>();
            providerLocatorFactoryMock.Setup(x =>
                    x.Instantiate(It.IsAny<string>())
                ).Returns(providerLocatorMock.Object);

            var mapper = new OleDbMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()), providerLocatorFactoryMock.Object);
            var result = mapper.Map(urlInfo);

            providerLocatorFactoryMock.Verify(x => x.Instantiate("myprovider"));
            providerLocatorMock.Verify(x => x.Locate(), Times.Once);
            providerLocatorMock.Verify(x => x.AdditionalMappers, Times.Once);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(OleDbMapper.PROVIDER_KEYWORD));
            Assert.That(result[OleDbMapper.PROVIDER_KEYWORD], Is.EqualTo("My provider"));
        }
    }
}
