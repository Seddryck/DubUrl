using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Rewriting.Implementation;
using Vertica.Data.VerticaClient;

namespace DubUrl.Testing.Rewriting.Implementation;

public class VerticaRewriterTest
{
    private const string PROVIDER_NAME = "VerticaConnector";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, VerticaObjectFactory.Instance);

    [Test]
    [TestCase("host", "host")]
    [TestCase("host", "host", "db", 1234)]
    public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new VerticaRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(VerticaRewriter.SERVER_KEYWORD));
        Assert.That(result[VerticaRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new VerticaRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(VerticaRewriter.DATABASE_KEYWORD));
        Assert.That(result[VerticaRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }


    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"] };
        var rewriter = new VerticaRewriter(ConnectionStringBuilder);
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(VerticaRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[VerticaRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(VerticaRewriter.PASSWORD_KEYWORD));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result[VerticaRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey(VerticaRewriter.SSPI_KEYWORD));
        });
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = ["db"] };
        var rewriter = new VerticaRewriter(ConnectionStringBuilder);
        var result = rewriter.Execute(urlInfo);

        Assert.That(result, Does.ContainKey(VerticaRewriter.SSPI_KEYWORD));
        Assert.That(result[VerticaRewriter.SSPI_KEYWORD], Is.EqualTo("True"));
    }

    [Test]
    public void Map_UrlInfo_Options()
    {
        var urlInfo = new UrlInfo() { Username = "user", Segments = ["db"] };
        urlInfo.Options.Add("Label", "myApp");
        urlInfo.Options.Add("ReadOnly", "true");

        var Rewriter = new VerticaRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Label"));
        Assert.Multiple(() =>
        {
            Assert.That(result["Label"], Is.EqualTo("myApp"));
            Assert.That(result, Does.ContainKey("ReadOnly"));
        });
        Assert.That(result["ReadOnly"], Is.EqualTo("true"));
    }
}
