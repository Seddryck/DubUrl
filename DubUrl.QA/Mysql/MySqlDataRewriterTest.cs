using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using MySql.Data.MySqlClient;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.QA.Mysql;

[Category("MySql")]
[Category("ConnectionString")]
public class MySqlDataRewriterTest
{
    private const string PROVIDER_NAME = "Mysql";

    protected virtual DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, MySqlClientFactory.Instance);

    [Test]
    [TestCase("host", "host")]
    [TestCase("host", "host", "db", 1234)]
    public void Map_UrlInfo_DataSource(string expected, string host = "host", string segmentsList = "db", int port = 0)
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new MySqlDataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(MySqlDataRewriter.SERVER_KEYWORD));
        Assert.That(result[MySqlDataRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Username = "user" };
        var Rewriter = new MySqlDataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(MySqlDataRewriter.DATABASE_KEYWORD));
        Assert.That(result[MySqlDataRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }


    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"] };
        var Rewriter = new MySqlDataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(MySqlDataRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[MySqlDataRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(MySqlDataRewriter.PASSWORD_KEYWORD));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result[MySqlDataRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.Not.ContainKey("Integrated Security"));
        });
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "", Password = "", Segments = ["db"] };
        var Rewriter = new MySqlDataRewriter(ConnectionStringBuilder);
        Assert.Catch<PlatformNotSupportedException>(() => Rewriter.Execute(urlInfo));
    }

    [Test]
    public void Map_UrlInfo_Options()
    {
        var urlInfo = new UrlInfo() { Username = "user", Segments = ["db"] };
        urlInfo.Options.Add("SslCa", "myCert");
        urlInfo.Options.Add("Persist Security Info", "true");

        var Rewriter = new MySqlDataRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("certificatefile"));
        Assert.Multiple(() =>
        {
            Assert.That(result["certificatefile"], Is.EqualTo("myCert"));
            Assert.That(result, Does.ContainKey("persistsecurityinfo"));
        });
        Assert.That(result["persistsecurityinfo"], Is.True);
    }
}
