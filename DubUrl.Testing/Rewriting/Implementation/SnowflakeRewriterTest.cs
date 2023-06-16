using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Snowflake.Data.Client;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.Testing.Rewriting.Implementation
{
    public class SnowflakeRewriterTest
    {
        private const string PROVIDER_NAME = "Snowflake.Data";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SnowflakeDbFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        public void Map_UrlInfo_Account(string expected, string host = "host")
        {
            var urlInfo = new UrlInfo() { Host = host };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.SERVER_KEYWORD));
            Assert.That(result[SnowflakeRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.DATABASE_KEYWORD));
            Assert.That(result[SnowflakeRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoDatabaseMissing_Database()
        {
            var urlInfo = new UrlInfo() { Segments = Array.Empty<string>() };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(SnowflakeRewriter.DATABASE_KEYWORD));
        }

        [Test]
        public void Map_UrlInfoWithSchema_DatabaseAndSchema()
        {
            var urlInfo = new UrlInfo() { Segments = "db/schema".Split('/') };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.DATABASE_KEYWORD));
            Assert.That(result[SnowflakeRewriter.DATABASE_KEYWORD], Is.EqualTo("db"));
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.SCHEMA_KEYWORD));
            Assert.That(result[SnowflakeRewriter.SCHEMA_KEYWORD], Is.EqualTo("schema"));
        }

        [Test]
        public void Map_UrlInfoSchemaMissing_Database()
        {
            var urlInfo = new UrlInfo() { Segments = string.Empty.Split('/') };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(SnowflakeRewriter.SCHEMA_KEYWORD));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            var Rewriter = new SnowflakeRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.USERNAME_KEYWORD));
            Assert.That(result[SnowflakeRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(SnowflakeRewriter.PASSWORD_KEYWORD));
            Assert.That(result[SnowflakeRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }
    }
}