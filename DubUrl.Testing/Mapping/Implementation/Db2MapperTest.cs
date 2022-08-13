using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using IBM.Data.DB2.Core;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialecting;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class Db2MapperTest
    {
        private const string PROVIDER_NAME = "IBM.Data.DB2.Core";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, DB2Factory.Instance);
        }

        [Test]
        [TestCase("host", "host", 0)]
        [TestCase("host:1234", "host", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", int port = 0, string segmentsList = "db")
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new Db2Mapper(ConnectionStringBuilder, new Db2Dialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(Db2Mapper.SERVER_KEYWORD));
            Assert.That(result[Db2Mapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new Db2Mapper(ConnectionStringBuilder, new Db2Dialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(Db2Mapper.DATABASE_KEYWORD));
            Assert.That(result[Db2Mapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase()]
        public void Map_UrlInfoWithUsernamePassword_Authentication(string segmentsList = "db")
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = segmentsList.Split('/') };
            var mapper = new Db2Mapper(ConnectionStringBuilder, new Db2Dialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(Db2Mapper.USERNAME_KEYWORD));
            Assert.That(result[Db2Mapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(Db2Mapper.PASSWORD_KEYWORD));
            Assert.That(result[Db2Mapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }
    }
}