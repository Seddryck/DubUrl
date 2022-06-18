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
using MySql.Data.MySqlClient;

namespace DubUrl.Testing.Mapping
{
    public class MySqlDataMapperTest
    {
        private const string PROVIDER_NAME = "Mysql";

        private DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, MySqlClientFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host", "host", "db", 1234)]
        public void UrlInfo_Map_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("server"));
            Assert.That(result["server"], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("database"));
            Assert.That(result["database"], Is.EqualTo(expected));
        }


        [Test]
        public void UrlInfoWithUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("user id"));
            Assert.That(result["user id"], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey("password"));
            Assert.That(result["password"], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("Integrated Security"));
        }

        [Test]
        public void UrlInfoWithoutUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new PgsqlMapper(ConnectionStringBuilder);
            Assert.Catch<PlatformNotSupportedException>(() => mapper.Map(urlInfo));
        }

        [Test]
        public void UrlInfo_Map_Options()
        {
            var urlInfo = new UrlInfo() { Username="user", Segments = new[] { "db" } };
            urlInfo.Options.Add("SslCa", "myCert");
            urlInfo.Options.Add("Persist Security Info", "true");

            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("certificatefile"));
            Assert.That(result["certificatefile"], Is.EqualTo("myCert"));
            Assert.That(result, Does.ContainKey("persistsecurityinfo"));
            Assert.That(result["persistsecurityinfo"], Is.True);
        }
    }
}
