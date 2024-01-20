using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using Npgsql;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Rewriting.Implementation;

public class CockRoachRewriterTest
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
        var mapper = new CockRoachRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

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
        var mapper = new CockRoachRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.PORT_KEYWORD));
        Assert.That(result[PostgresqlRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoDefautPort_Port()
    {
        var urlInfo = new UrlInfo() { Host = "host", Segments = "db".Split('/') };
        var mapper = new CockRoachRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.PORT_KEYWORD));
        Assert.That(result[PostgresqlRewriter.PORT_KEYWORD], Is.EqualTo(26257));
    }


    [Test]
    [TestCase("db", "db")]
    public void Map_UrlInfo_Database(string segmentsList, string expected)
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
        var mapper = new CockRoachRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.DATABASE_KEYWORD));
        Assert.That(result[PostgresqlRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"] };
        var rewriter = new CockRoachRewriter(ConnectionStringBuilder);
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PostgresqlRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[PostgresqlRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(PostgresqlRewriter.PASSWORD_KEYWORD));
        });
        Assert.That(result[PostgresqlRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
        if (rewriter.IsIntegratedSecurityAllowed)
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
        var rewriter = new CockRoachRewriter(ConnectionStringBuilder);
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.USERNAME_KEYWORD));
        Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.PASSWORD_KEYWORD));
        if (rewriter.IsIntegratedSecurityAllowed)
        {
            Assert.That(result, Does.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
            Assert.That(result[PostgresqlRewriter.SSPI_KEYWORD], Is.EqualTo("sspi").Or.True);
        }
        else
            Assert.That(result, Does.Not.ContainKey(PostgresqlRewriter.SSPI_KEYWORD));
    }

    [Test]
    public void UrlInfo_Map_Options()
    {
        var urlInfo = new UrlInfo() { Segments = ["db"] };
        urlInfo.Options.Add("Application Name", "myApp");
        urlInfo.Options.Add("Persist Security Info", "true");

        var mapper = new CockRoachRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Application Name"));
        Assert.Multiple(() =>
        {
            Assert.That(result["Application Name"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("Persist Security Info"));
        });
        Assert.That(result["Persist Security Info"], Is.True);
    }
}
