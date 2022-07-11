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
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlDataMapper.SERVER_KEYWORD));
            Assert.That(result[MySqlDataMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlDataMapper.DATABASE_KEYWORD));
            Assert.That(result[MySqlDataMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlDataMapper.USERNAME_KEYWORD));
            Assert.That(result[MySqlDataMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(MySqlDataMapper.PASSWORD_KEYWORD));
            Assert.That(result[MySqlDataMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("Integrated Security"));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new MySqlDataMapper(ConnectionStringBuilder);
            Assert.Catch<PlatformNotSupportedException>(() => mapper.Map(urlInfo));
        }

        [Test]
        public void Map_UrlInfo_Options()
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
