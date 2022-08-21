using DubUrl.Mapping.Implementation;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class MsSqlServerMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.SqlClient";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqlClientFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host\\instance", "host", "instance/db")]
        [TestCase("host\\instance,1234", "host", "instance/db", 1234)]
        [TestCase("host,1234", "host", "db", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.SERVER_KEYWORD));
            Assert.That(result[MsSqlServerMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        [TestCase("instance/db")]
        public void Map_UrlInfo_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.DATABASE_KEYWORD));
            Assert.That(result[MsSqlServerMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.USERNAME_KEYWORD));
            Assert.That(result[MsSqlServerMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.PASSWORD_KEYWORD));
            Assert.That(result[MsSqlServerMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.SSPI_KEYWORD));
            Assert.That(result[MsSqlServerMapper.SSPI_KEYWORD], Is.EqualTo(false));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.USERNAME_KEYWORD));
            Assert.That(result[MsSqlServerMapper.USERNAME_KEYWORD], Is.Empty);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.PASSWORD_KEYWORD));
            Assert.That(result[MsSqlServerMapper.PASSWORD_KEYWORD], Is.Empty);
            Assert.That(result, Does.ContainKey(MsSqlServerMapper.SSPI_KEYWORD));
            Assert.That(result[MsSqlServerMapper.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("ConnectRetryCount", "5");

            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Application Name"));
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("ConnectRetryCount"));
            Assert.That(result["ConnectRetryCount"], Is.EqualTo(5));
        }

        [Test]
        public void GetDialect_None_DialectReturned()
        {
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(new[] { "ms","mssql" }));
            var result = mapper.GetDialect();

            Assert.That(result, Is.Not.Null.Or.Empty);
            Assert.IsInstanceOf<MssqlDialect>(result);
            Assert.That(result.Aliases, Does.Contain("mssql"));
            Assert.That(result.Aliases, Does.Contain("ms"));
        }
    }
}
