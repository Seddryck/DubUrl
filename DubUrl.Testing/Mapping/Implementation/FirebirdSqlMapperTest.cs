using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using FirebirdSql.Data.FirebirdClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialecting;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class FirebirdSqlMapperTest
    {
        private const string PROVIDER_NAME = "FirebirdSql.Data.FirebirdClient";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, FirebirdClientFactory.Instance);
        }

        [Test]
        [TestCase("localhost", "data.fdb")]
        [TestCase(".", "data.fdb")]
        [TestCase("", "data.fdb")]
        public void Map_DoubleSlash_DataSource(string host, string segmentsList, string expected = "localhost")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.SERVER_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("data.fdb")]
        public void Map_DoubleSlash_Database(string host, string expected = "data.fdb")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = Array.Empty<string>() };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.SERVER_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.SERVER_KEYWORD], Is.EqualTo("localhost"));
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.DATABASE_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }


        [Test]
        [TestCase("localhost", "directory/data.fdb")]
        [TestCase(".", "directory/data.fdb")]
        [TestCase("", "directory/data.fdb")]
        public void Map_TripleSlash_DataSource(string host, string segmentsList, string expected = "directory/data.fdb")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.DATABASE_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.DATABASE_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
        }


        [Test]
        public void Map_QuadrupleSlash_DataSource()
        {
            var path = "c:/directory/data.fdb";
            var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/') };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.DATABASE_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.DATABASE_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.USERNAME_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.PASSWORD_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }

        [Test]
        public void Map_UrlInfoWithPort_Port()
        {
            var urlInfo = new UrlInfo() { Host = "localhost", Port = 3001, Segments = new[] { "db" } };
            var mapper = new FirebirdSqlMapper(ConnectionStringBuilder, new FirebirdSqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(FirebirdSqlMapper.PORT_KEYWORD));
            Assert.That(result[FirebirdSqlMapper.PORT_KEYWORD], Is.EqualTo("3001"));
        }

    }
}
