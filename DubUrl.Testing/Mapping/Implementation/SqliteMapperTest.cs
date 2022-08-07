using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using DubUrl.Mapping.Implementation;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class SqliteMapperTest
    {
        private const string PROVIDER_NAME = "Microsoft.Data.Sqlite";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqliteFactory.Instance);
        }

        [Test]
        [TestCase("data.db", "")]
        [TestCase("localhost", "data.db")]
        [TestCase(".", "data.db")]
        [TestCase("", "data.db")]
        public void Map_DoubleSlash_DataSource(string host, string segmentsList, string expected = "data.db")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
            var mapper = new SqliteMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SqliteMapper.DATABASE_KEYWORD));
            Assert.That(result[SqliteMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("localhost", "directory/data.db")]
        [TestCase(".", "directory/data.db")]
        [TestCase("", "directory/data.db")]
        public void Map_TripleSlash_DataSource(string host, string segmentsList, string expected = "directory/data.db")
        {
            var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/') };
            var mapper = new SqliteMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SqliteMapper.DATABASE_KEYWORD));
            Assert.That(result[SqliteMapper.DATABASE_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
        }


        [Test]
        public void Map_QuadrupleSlash_DataSource()
        {
            var path = "c:/directory/data.db";
            var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/') };
            var mapper = new SqliteMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(SqliteMapper.DATABASE_KEYWORD));
            Assert.That(result[SqliteMapper.DATABASE_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
