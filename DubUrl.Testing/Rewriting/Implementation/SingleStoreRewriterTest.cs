using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using MySqlConnector;
using DubUrl.Rewriting.Implementation;
using DubUrl.Rewriting;
using SingleStoreConnector;

namespace DubUrl.Testing.Rewriting.Implementation;

public class SingleStoreRewriterTest
{
    private const string PROVIDER_NAME = "SingleStoreConnector";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SingleStoreConnectorFactory.Instance);

    [Test]
    [TestCase("host", "host")]
    [TestCase("host", "host", "db", 1234)]
    public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new SingleStoreRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SingleStoreRewriter.SERVER_KEYWORD));
        Assert.That(result[SingleStoreRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new SingleStoreRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SingleStoreRewriter.DATABASE_KEYWORD));
        Assert.That(result[SingleStoreRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }


    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"] };
        var Rewriter = new SingleStoreRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SingleStoreRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[SingleStoreRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(SingleStoreRewriter.PASSWORD_KEYWORD));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result[SingleStoreRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("Integrated Security"));
        });
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = ["db"] };
        var Rewriter = new SingleStoreRewriter(ConnectionStringBuilder);
        Assert.Catch<UsernameNotFoundException>(() => Rewriter.Execute(urlInfo));
    }

    [Test]
    public void Map_UrlInfo_Options()
    {
        var urlInfo = new UrlInfo() { Username = "user", Segments = ["db"] };
        urlInfo.Options.Add("Application Name", "myApp");
        urlInfo.Options.Add("Persist Security Info", "true");

        var Rewriter = new SingleStoreRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

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
