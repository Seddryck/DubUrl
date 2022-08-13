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
using DubUrl.Querying.Dialecting;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class SnowflakeMapperTest
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
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeMapper.SERVER_KEYWORD));
            Assert.That(result[SnowflakeMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeMapper.DATABASE_KEYWORD));
            Assert.That(result[SnowflakeMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoDatabaseMissing_Database()
        {
            var urlInfo = new UrlInfo() { Segments = Array.Empty<string>() };
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(SnowflakeMapper.DATABASE_KEYWORD));
        }

        [Test]
        public void Map_UrlInfoWithSchema_DatabaseAndSchema()
        {
            var urlInfo = new UrlInfo() { Segments = "db/schema".Split('/') };
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeMapper.DATABASE_KEYWORD));
            Assert.That(result[SnowflakeMapper.DATABASE_KEYWORD], Is.EqualTo("db"));
            Assert.That(result, Does.ContainKey(SnowflakeMapper.SCHEMA_KEYWORD));
            Assert.That(result[SnowflakeMapper.SCHEMA_KEYWORD], Is.EqualTo("schema"));
        }

        [Test]
        public void Map_UrlInfoSchemaMissing_Database()
        {
            var urlInfo = new UrlInfo() { Segments = string.Empty.Split('/') };
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(SnowflakeMapper.SCHEMA_KEYWORD));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            var mapper = new SnowflakeMapper(ConnectionStringBuilder, new SnowflakeDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SnowflakeMapper.USERNAME_KEYWORD));
            Assert.That(result[SnowflakeMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(SnowflakeMapper.PASSWORD_KEYWORD));
            Assert.That(result[SnowflakeMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }
    }
}