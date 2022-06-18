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

namespace DubUrl.Testing.Mapping
{
    public class MssqlMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.SqlClient";

        private DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqlClientFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host\\instance", "host", "instance/db")]
        [TestCase("host\\instance,1234", "host", "instance/db", 1234)]
        [TestCase("host,1234", "host", "db", 1234)]
        public void UrlInfo_Map_DataSource(string expected, string host = "host", string segmentsList = "db", int port=0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new MssqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);
            
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Data Source"));
            Assert.That(result["Data Source"], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        [TestCase("instance/db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected="db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new MssqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Initial Catalog"));
            Assert.That(result["Initial Catalog"], Is.EqualTo(expected));
        }


        [Test]
        public void UrlInfoWithUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username="user", Password="pwd", Segments=new[] {"db"} };
            var mapper = new MssqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("User ID"));
            Assert.That(result["User ID"], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey("Password"));
            Assert.That(result["Password"], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey("Integrated Security"));
            Assert.That(result["Integrated Security"], Is.EqualTo(false));
        }

        [Test]
        public void UrlInfoWithoutUsernamePassword_Map_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new MssqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("User ID"));
            Assert.That(result["User ID"], Is.Empty);
            Assert.That(result, Does.ContainKey("Password"));
            Assert.That(result["Password"], Is.Empty);
            Assert.That(result, Does.ContainKey("Integrated Security"));
            Assert.That(result["Integrated Security"], Is.EqualTo("sspi").Or.True);
        }

        [Test]
        public void UrlInfo_Map_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("ConnectRetryCount", "5");

            var mapper = new MssqlMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Application Name"));
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("ConnectRetryCount"));
            Assert.That(result["ConnectRetryCount"], Is.EqualTo(5));
        }
    }
}
