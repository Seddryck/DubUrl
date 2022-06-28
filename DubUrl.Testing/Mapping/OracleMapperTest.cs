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
using Oracle.ManagedDataAccess.Client;

namespace DubUrl.Testing.Mapping
{
    public class OracleMapperTest
    {
        private const string PROVIDER_NAME = "Oracle";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OracleClientFactory.Instance);
        }

        [Test]
        [TestCase("tns-alias", "tns-alias")]
        [TestCase("(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=host)(PORT=1234))(CONNECT_DATA=(SERVICE_NAME=service-name)))", "host", "service-name", 1234)]
        public void UrlInfo_Map_DataSource(string expected, string host = "host", string segmentsList="", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments= string.IsNullOrEmpty(segmentsList) ? Array.Empty<string>() : segmentsList.Split("/") };
            var mapper = new OracleMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("DATA SOURCE"));
            Assert.That(result["DATA SOURCE"], Is.EqualTo(expected));
        }

        [Test]
        public void UrlInfoWithUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            var mapper = new OracleMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("USER ID"));
            Assert.That(result["USER ID"], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey("PASSWORD"));
            Assert.That(result["PASSWORD"], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("INTEGRATED SECURITY"));
        }

        [Test]
        public void UrlInfoWithoutUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new OracleMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("USER ID"));
            Assert.That(result["USER ID"], Is.EqualTo("/"));
            Assert.That(result, Does.ContainKey("PASSWORD"));
            Assert.That(result["PASSWORD"], Is.Empty);
        }

        [Test]
        public void UrlInfo_Map_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Statement Cache Size", "100");
            urlInfo.Options.Add("Persist Security Info", "true");

            var mapper = new OracleMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("STATEMENT CACHE SIZE"));
            Assert.That(result["STATEMENT CACHE SIZE"], Is.EqualTo(100));
            Assert.That(result, Does.ContainKey("PERSIST SECURITY INFO"));
            Assert.That(result["PERSIST SECURITY INFO"], Is.True);
        }
    }
}
