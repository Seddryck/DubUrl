using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using MySqlConnector;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting;

namespace DubUrl.Testing.Rewriting.Implementation
{
    public class MySqlConnectorRewriterTest
    {
        private const string PROVIDER_NAME = "MySqlConnector";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, MySqlConnectorFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host", "host", "db", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
            var Rewriter = new MySqlConnectorRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlConnectorRewriter.SERVER_KEYWORD));
            Assert.That(result[MySqlConnectorRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
            var Rewriter = new MySqlConnectorRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlConnectorRewriter.DATABASE_KEYWORD));
            Assert.That(result[MySqlConnectorRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var Rewriter = new MySqlConnectorRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MySqlConnectorRewriter.USERNAME_KEYWORD));
            Assert.That(result[MySqlConnectorRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(MySqlConnectorRewriter.PASSWORD_KEYWORD));
            Assert.That(result[MySqlConnectorRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("Integrated Security"));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var Rewriter = new MySqlConnectorRewriter(ConnectionStringBuilder);
            Assert.Catch<UsernameNotFoundException>(() => Rewriter.Execute(urlInfo));
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Username = "user", Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("Persist Security Info", "true");

            var Rewriter = new MySqlConnectorRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Application Name"));
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("Persist Security Info"));
            Assert.That(result["Persist Security Info"], Is.True);
        }
    }
}
