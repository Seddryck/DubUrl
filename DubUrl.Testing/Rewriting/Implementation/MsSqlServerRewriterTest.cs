using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace DubUrl.Testing.Rewriting.Implementation
{
    public class MsSqlServerRewriterTest
    {
        private const string PROVIDER_NAME = "Microsoft.Data.SqlClient";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqlClientFactory.Instance);
        }

        [Test]
        [TestCase("host", "host")]
        [TestCase("host\\instance", "host", "instance/db")]
        [TestCase("host\\instance,1234", "host", "instance/db", 1234)]
        [TestCase("host,1234", "host", "db", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
            var Rewriter = new MsSqlServerRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerRewriter.SERVER_KEYWORD));
            Assert.That(result[MsSqlServerRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        [TestCase("db")]
        [TestCase("instance/db")]
        public void Map_UrlInfo_InitialCatalog(string segmentsList = "db", string expected = "db")
        {
            var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
            var Rewriter = new MsSqlServerRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey(MsSqlServerRewriter.DATABASE_KEYWORD));
            Assert.That(result[MsSqlServerRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
        }


        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" } };
            var Rewriter = new MsSqlServerRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.USERNAME_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.PASSWORD_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.SSPI_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.SSPI_KEYWORD], Is.EqualTo(false));
            });

        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var Rewriter = new MsSqlServerRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.USERNAME_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.USERNAME_KEYWORD], Is.Empty);
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.PASSWORD_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.PASSWORD_KEYWORD], Is.Empty);
                Assert.That(result, Does.ContainKey(MsSqlServerRewriter.SSPI_KEYWORD));
                Assert.That(result[MsSqlServerRewriter.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
            });
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Application Name", "myApp");
            urlInfo.Options.Add("Enlist", "False");

            var Rewriter = new MsSqlServerRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(result, Does.ContainKey("Application Name"));
                Assert.That(result["Application Name"], Is.EqualTo("myApp"));
                Assert.That(result, Does.ContainKey("Enlist"));
                Assert.That(result["Enlist"], Is.EqualTo(false));
            });
        }

        
    }
}
