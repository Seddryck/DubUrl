using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;



namespace DubUrl.Testing.Rewriting.Implementation
{
    public class OracleManagedDataAccessRewriterTest
    {
        private const string PROVIDER_NAME = "Oracle.ManagedDataAccess";

        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OracleClientFactory.Instance);
        }

        [Test]
        [TestCase("tns-alias", "tns-alias")]
        [TestCase("(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=host)(PORT=1234))(CONNECT_DATA=(SERVICE_NAME=service-name)))", "host", "service-name", 1234)]
        public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "", int port = 0)
        {
            var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = string.IsNullOrEmpty(segmentsList) ? Array.Empty<string>() : segmentsList.Split("/") };
            var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.DATASOURCE_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.DATASOURCE_KEYWORD], Is.EqualTo(expected));
        }

        [Test]
        public void Map_UrlInfoWithUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "user", Password = "pwd" };
            var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.USERNAME_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.PASSWORD_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("INTEGRATED SECURITY"));
        }

        [Test]
        public void Map_UrlInfoWithoutUsernamePassword_Authentication()
        {
            var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = new[] { "db" } };
            var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.USERNAME_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.USERNAME_KEYWORD], Is.EqualTo("/"));
            Assert.That((IReadOnlyDictionary<string, object>)result, Does.ContainKey(OracleManagedDataAccessRewriter.PASSWORD_KEYWORD));
            Assert.That((object)result[OracleManagedDataAccessRewriter.PASSWORD_KEYWORD], Is.Empty);
        }

        [Test]
        public void Map_UrlInfo_Options()
        {
            var urlInfo = new UrlInfo() { Segments = new[] { "db" } };
            urlInfo.Options.Add("Statement Cache Size", "100");
            urlInfo.Options.Add("Persist Security Info", "true");

            var Rewriter = new OracleManagedDataAccessRewriter(ConnectionStringBuilder);
            var result = Rewriter.Execute(urlInfo);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Does.ContainKey("STATEMENT CACHE SIZE"));
            Assert.That(result["STATEMENT CACHE SIZE"], Is.EqualTo(100));
            Assert.That(result, Does.ContainKey("PERSIST SECURITY INFO"));
            Assert.That(result["PERSIST SECURITY INFO"], Is.True);
        }
    }
}
