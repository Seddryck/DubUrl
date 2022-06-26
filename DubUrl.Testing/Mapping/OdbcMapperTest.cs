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

namespace DubUrl.Testing.Mapping
{
    public class OdbcMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.Odbc";

        private DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host", "host", "db", 1234)]
        public void UrlInfo_Map_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Server"));
            Assert.That(result["Server"], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Database"));
            Assert.That(result["Database"], Is.EqualTo(expected));
        }


        [Test]
        public void UrlInfoWithUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Uid"));
            Assert.That(result["Uid"], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey("Pwd"));
            Assert.That(result["Pwd"], Is.EqualTo("pwd"));
        }

        [Test]
        public void UrlInfo_Map_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
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
        public void UrlInfo_Map_Driver(string driver)
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Driver", driver);

            var mapper = new OdbcMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Driver"));
            Assert.That(result["Driver"], Is.EqualTo("{MySQL ODBC 5.1 Driver}"));
        }
    }
}
