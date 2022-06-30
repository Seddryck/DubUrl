using DubUrl.Mapping;
using DubUrl.Parsing;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Teradata.Client;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping
{
    public class TeradataMapperTest
    {
        private const string PROVIDER_NAME = "Teradata.Client";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, Teradata.Client.Provider.TdFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        public void Map_UrlInfo_DataSource(string expected, string host = "host")
        {
            var urlInfo = new UrlInfo() { Host = host };
            var mapper = new TeradataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(TeradataMapper.SERVER_KEYWORD));
            Assert.That(result[TeradataMapper.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase(453, 453)]
        public void Map_UrlInfo_PortNumber(int expected, int port)
        {
            var urlInfo = new UrlInfo() { Port = port };
            var mapper = new TeradataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(TeradataMapper.PORT_KEYWORD));
            Assert.That(result[TeradataMapper.PORT_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var mapper = new TeradataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(TeradataMapper.DATABASE_KEYWORD));
            Assert.That(result[TeradataMapper.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd"};
            var mapper = new TeradataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(TeradataMapper.USERNAME_KEYWORD));
            Assert.That(result[TeradataMapper.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(TeradataMapper.PASSWORD_KEYWORD));
            Assert.That(result[TeradataMapper.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey(TeradataMapper.SSPI_KEYWORD));
            Assert.That(result[TeradataMapper.SSPI_KEYWORD], Is.EqualTo(false));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() {};
            var mapper = new TeradataMapper(ConnectionStringBuilder);
            var result = mapper.Map(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result[TeradataMapper.USERNAME_KEYWORD], Is.Null.Or.Empty);
            Assert.That(result[TeradataMapper.PASSWORD_KEYWORD], Is.Null.Or.Empty);
            Assert.That(result, Does.ContainKey(TeradataMapper.SSPI_KEYWORD));
            Assert.That(result[TeradataMapper.SSPI_KEYWORD], Is.EqualTo(true));
        }
    }
}