using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using Npgsql;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting;

namespace DubUrl.Testing.Rewriting.Implementation
{
    public class QuestDbRewriterTest
    {
        private const string PROVIDER_NAME = "Npgsql";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, NpgsqlFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "qdb", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user", Password = "pwd" };
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(QuestDbRewriter.SERVER_KEYWORD));
            Assert.That(result[QuestDbRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("host", 1234, 1234)]
        [TestCase("host", 0, 8812)]
        public void Map_UrlInfo_Port(string host, int port, int expected)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Username = "user", Password = "pwd" };
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(QuestDbRewriter.PORT_KEYWORD));
            Assert.That(result[QuestDbRewriter.PORT_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("qdb", "qdb")]
        [TestCase("", "qdb")]
        public void UrlInfo_Map_InitialCatalog(string segmentsList, string expected)
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user", Password = "pwd" };
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(QuestDbRewriter.DATABASE_KEYWORD));
            Assert.That(result[QuestDbRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("anydb")]
        public void Map_UnexpectedUrlInfo_Throws(string segmentsList)
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var ex = Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd"};
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(QuestDbRewriter.USERNAME_KEYWORD));
            Assert.That(result[QuestDbRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(QuestDbRewriter.PASSWORD_KEYWORD));
            Assert.That(result[QuestDbRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        }

        [Test]
        [TestCase("user", "")]
        [TestCase("", "pwd")]
        [TestCase("", "")]
        public void Map_UrlInfoWithoutUsernamePassword_Throws(string username, string password)
        {
            var urlInfo = new UrlInfo() { Username = username, Password = password };
            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var ex = Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
        }

        [Test]
        [TestCase("statement_timeout")]
        [TestCase("Command Timeout")]
        public void Map_UrlInfo_Options(string optionName)
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            urlInfo.Options.Add(optionName, "60000");

            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Command Timeout"));
            Assert.That(result["Command Timeout"], Is.EqualTo(60000));
        }

        [Test]
        public void Map_UrlInfoServerCompatibilityMode_Options()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            urlInfo.Options.Add("ServerCompatibilityMode", "NoTypeLoading");

            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Server Compatibility Mode"));
            Assert.That(result["Server Compatibility Mode"], Is.EqualTo(ServerCompatibilityMode.NoTypeLoading));
        }

        [Test]
        public void Map_UrlInfoServerCompatibilityModeNotSpecified_Options()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };

            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("Server Compatibility Mode"));
            Assert.That(result["Server Compatibility Mode"], Is.EqualTo(ServerCompatibilityMode.NoTypeLoading));
        }

        [Test]
        public void Map_UnexpectedServerCompatibilityMode_Throws()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            urlInfo.Options.Add("ServerCompatibilityMode", "ANY VALUE");

            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var ex = Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
        }

        [Test]
        public void Map_UnexpectedOption_Throws()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            urlInfo.Options.Add("any_other_option", "value");

            var Rewriter = new QuestDbRewriter(ConnectionStringBuilder);
            var ex = Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
        }
    }
}
