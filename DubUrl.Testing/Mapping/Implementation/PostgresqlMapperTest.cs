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
using DubUrl.Querying.Dialecting;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class PostrgresqlMapperTest
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
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.SERVER_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase(1234, 1234, "host")]
        [TestCase(4567, 4567, "host", "db")]
        public void Map_UrlInfo_Port(int expected, int port = 0, string host = "host", string segmentsList = "db")
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.PORT_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.PORT_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.DATABASE_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.USERNAME_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.PASSWORD_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.SSPI_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.SSPI_KEYWORD], Is.EqualTo(false));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.Not.ContainKey(PostgresqlMapper.USERNAME_KEYWORD));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.Not.ContainKey(PostgresqlMapper.PASSWORD_KEYWORD));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(PostgresqlMapper.SSPI_KEYWORD));
            Assert.That((object)result[PostgresqlMapper.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("Persist Security Info", "true");

            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(Array.Empty<string>()));
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Application Name"));
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("Persist Security Info"));
            Assert.That(result["Persist Security Info"], Is.True);
        }


        [Test]
        public void GetDialect_None_DialectReturned()
        {
            var mapper = new PostgresqlMapper(ConnectionStringBuilder, new PgsqlDialect(new[] { "pg", "pgsql" }));
            var result = mapper.GetDialect();

            Assert.That(result, Is.Not.Null.Or.Empty);
            Assert.IsInstanceOf<PgsqlDialect>(result);
            Assert.That(result.Aliases, Does.Contain("pgsql"));
            Assert.That(result.Aliases, Does.Contain("pg"));
        }
    }
}
