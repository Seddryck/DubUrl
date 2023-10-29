using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using Npgsql;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting;

namespace DubUrl.Testing.Rewriting.Implementation
{
    public class GlareDbRewriterTest
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
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username="User", Password="P@ssw0rd" };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(GlareDbRewriter.SERVER_KEYWORD));
            Assert.That(result[GlareDbRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase(1234, 1234, "host")]
        [TestCase(4567, 4567, "host", "db")]
        public void Map_UrlInfo_Port(int expected, int port = 0, string host = "host", string segmentsList = "db")
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "User", Password = "P@ssw0rd" };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(GlareDbRewriter.PORT_KEYWORD));
            Assert.That(result[GlareDbRewriter.PORT_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "User", Password = "P@ssw0rd" };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(GlareDbRewriter.DATABASE_KEYWORD));
            Assert.That(result[GlareDbRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(GlareDbRewriter.USERNAME_KEYWORD));
            Assert.That(result[GlareDbRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(GlareDbRewriter.PASSWORD_KEYWORD));
            Assert.That(result[GlareDbRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }

        [Test]
        [TestCase("localhost")]
        [TestCase("LocalHost")]
        [TestCase(".")]
        [TestCase("127.0.0.1")]
        public void Map_UrlInfoWithoutUsernamePasswordAndLocalHost_Authentication(string host)
        {
            var urlInfo = new UrlInfo() { Host=host, Username = "", Password = "", Segments = new[] { "db" } };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.Not.ContainKey(GlareDbRewriter.USERNAME_KEYWORD));
            Assert.That(result, Does.Not.ContainKey(GlareDbRewriter.PASSWORD_KEYWORD));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePasswordAndNotLocalHost_Authentication()
        {
            var urlInfo = new UrlInfo() { Host="Remote.Server" , Username = "", Password = "", Segments = new[] { "db" } };
            var Rewriter = new GlareDbRewriter(ConnectionStringBuilder);
            Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
        }
    }
}
