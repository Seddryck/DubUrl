using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using IBM.Data.Db2;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DubUrl.Testing.Rewriting.Implementation;

public class Db2RewriterTest
{
    private const string PROVIDER_NAME = "IBM.Data.DB2.Core";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, DB2Factory.Instance);
    }

    [Test]
    [TestCase("host", "host", 0)]
    [TestCase("host:1234", "host", 1234)]
    public void Map_UrlInfo_DataSource(string expected, string host = "host", int port = 0, string segmentsList = "db")
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/') };
        var Rewriter = new Db2Rewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(Db2Rewriter.SERVER_KEYWORD));
        Assert.That(result[Db2Rewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_Database(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/') };
        var Rewriter = new Db2Rewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(Db2Rewriter.DATABASE_KEYWORD));
        Assert.That(result[Db2Rewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase()]
    public void Map_UrlInfoWithUsernamePassword_Authentication(string segmentsList = "db")
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = segmentsList.Split('/') };
        var Rewriter = new Db2Rewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(Db2Rewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[Db2Rewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(Db2Rewriter.PASSWORD_KEYWORD));
        });
        Assert.That(result[Db2Rewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
    }
}