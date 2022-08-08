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
using DubUrl.Mapping.Implementation;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class PgsqlMapperTest
    {
        private const string PROVIDER_NAME = "Npgsql";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, NpgsqlFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host", "host", "db", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(PgsqlMapper.SERVER_KEYWORD));
            Assert.That(result[PgsqlMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase(1234, 1234, "host")]
        [TestCase(4567, 4567, "host", "db")]
        public void Map_UrlInfo_Port(int expected, int port = 0, string host = "host", string segmentsList = "db")
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(PgsqlMapper.PORT_KEYWORD));
            Assert.That(result[PgsqlMapper.PORT_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(PgsqlMapper.DATABASE_KEYWORD));
            Assert.That(result[PgsqlMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(PgsqlMapper.USERNAME_KEYWORD));
            Assert.That(result[PgsqlMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(PgsqlMapper.PASSWORD_KEYWORD));
            Assert.That(result[PgsqlMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey(PgsqlMapper.SSPI_KEYWORD));
            Assert.That(result[PgsqlMapper.SSPI_KEYWORD], Is.EqualTo(false));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(PgsqlMapper.USERNAME_KEYWORD));
            Assert.That(result, Does.Not.ContainKey(PgsqlMapper.PASSWORD_KEYWORD));
            Assert.That(result, Does.ContainKey(PgsqlMapper.SSPI_KEYWORD));
            Assert.That(result[PgsqlMapper.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("Persist Security Info", "true");

            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Application Name"));
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("Persist Security Info"));
            Assert.That(result["Persist Security Info"], Is.True);
        }
    }
}
