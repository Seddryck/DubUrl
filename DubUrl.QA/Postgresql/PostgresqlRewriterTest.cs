using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using Npgsql;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting.Tokening;

namespace DubUrl.QA.Postgresql;

[Category("Postgresql")]
[Category("ConnectionString")]
public class PostrgresqlRewriterTest
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
        var Rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.SERVER_KEYWORD));
        Assert.That(result[PostgresqlRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase(1234, 1234, "host")]
    [TestCase(4567, 4567, "host", "db")]
    public void Map_UrlInfo_Port(int expected, int port = 0, string host = "host", string segmentsList = "db")
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
        var Rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.PORT_KEYWORD));
        Assert.That(result[PostgresqlRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void UrlInfo_Map_InitialCatalog(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
        var Rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.DATABASE_KEYWORD));
        Assert.That(result[PostgresqlRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"] };
        var Rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[PostgresqlRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(PostgresqlRewriter.PASSWORD_KEYWORD));
        });
        Assert.That(result[PostgresqlRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        if (Rewriter.IsIntegratedSecurityAllowed)
        {
            Assert.That(result, Does.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
            Assert.That(result[PostgresqlRewriter.SSPI_KEYWORD], Is.EqualTo(false));
        }
        else
            Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = ["db"] };
        var Rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.USERNAME_KEYWORD));
        Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.PASSWORD_KEYWORD));
        if (Rewriter.IsIntegratedSecurityAllowed)
        {
            Assert.That(result, Does.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
            Assert.That(result[PostgresqlRewriter.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
        }
        else
            Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
    }

    [Test]
    public void Map_UrlInfo_Options()
    {
        var urlInfo = new UrlInfo() { Segments = ["db"] };
        urlInfo.Options.Add("Application Name", "myApp");
        urlInfo.Options.Add("Persist Security Info", "true");

        var rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Application Name"));
        Assert.Multiple(() =>
        {
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("Persist Security Info"));
        });
        Assert.That(result["Persist Security Info"], Is.True);
    }

    private class SslTokenMapper : BaseTokenMapper
    {
        public override void Execute(UrlInfo urlInfo)
        {
            if (urlInfo.Schemes.Contains("ssl", StringComparer.OrdinalIgnoreCase))
                Specificator.Execute("SSL Mode", "require");
        }
    }

    [Test]
    public void Prepend_SslTokenMapper_ConnectionString()
    {
        var urlInfo = new UrlInfo() { Schemes = ["ssl"], Host = "localhost", Segments = ["db"] };

        var rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        rewriter.Prepend(new SslTokenMapper());
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("SSL Mode"));
        Assert.That(result["SSL Mode"], Is.EqualTo(SslMode.Require));
    }

    [Test]
    public void Prepend_NoSslTokenMapper_ConnectionString()
    {
        var urlInfo = new UrlInfo() { Schemes = [], Host = "localhost", Segments = ["db"] };

        var rewriter = new PostgresqlRewriter(ConnectionStringBuilder);
        rewriter.Prepend(new SslTokenMapper());
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey("SSL Mode"));
    }
}
