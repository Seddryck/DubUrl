using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Testing.Rewriting;
using DubUrl.Adomd.Wrappers;
using DubUrl.Adomd.Rewriting;
using DubUrl.Rewriting;

namespace DubUrl.Adomd.Testing.Rewriting.Implementation;

public class SsasTabularRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.AnalysisServices.AdomdClient";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, AdomdFactory.Instance);

    [Test]
    [TestCase("localhost/db", "localhost")]
    [TestCase("localhost/~/db", "localhost")]
    [TestCase("localhost/~/db", "localhost")]
    [TestCase("localhost/~/db/cube", "localhost")]
    [TestCase("localhost:1234/~/db", "localhost:1234")]
    [TestCase("localhost:1234/~/db", "localhost:1234")]
    [TestCase("localhost:1234/~/db/cube", "localhost:1234")]
    [TestCase("localhost/instance/db", "localhost\\instance")]
    [TestCase("localhost/instance/db", "localhost\\instance")]
    [TestCase("localhost/instance/db/cube", "localhost\\instance")]
    public void Map_UrlInfo_DataSource(string input, string expected)
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new SsasTabularRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SsasRewriter.SERVER_KEYWORD));
        Assert.That(result[SsasRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("localhost")]
    [TestCase("localhost:1234")]
    public void Map_InvalidUrlInfo_Throws(string input)
        => Assert.That(() =>
            new SsasTabularRewriter(ConnectionStringBuilder)
                    .Execute(new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() })
            , Throws.TypeOf<InvalidConnectionUrlException>());

    [Test]
    [TestCase("localhost/db")]
    [TestCase("localhost/~/db")]
    [TestCase("localhost/~/db/cube")]
    [TestCase("localhost:1234/~/db")]
    [TestCase("localhost:1234/~/db/cube")]
    [TestCase("localhost/instance/db")]
    [TestCase("localhost/instance/db/cube")]
    public void Map_UrlInfo_InitialCatalog(string input, string expected = "db")
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new SsasTabularRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SsasRewriter.DATABASE_KEYWORD));
        Assert.That(result[SsasRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("localhost/~")]
    public void Map_InvalidUrlInfo_throws(string input)
        => Assert.That(() =>
            new SsasTabularRewriter(ConnectionStringBuilder)
                    .Execute(new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() })
            , Throws.TypeOf<InvalidConnectionUrlException>());

    [Test]
    [TestCase("localhost/~/db/cube")]
    [TestCase("localhost:1234/~/db/cube")]
    [TestCase("localhost/instance/db/cube")]
    [TestCase("localhost:1234/instance/db/cube")]
    public void Map_UrlInfo_Cube(string input, string expected = "cube")
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new SsasTabularRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SsasRewriter.CUBE_KEYWORD));
        Assert.That(result[SsasRewriter.CUBE_KEYWORD], Is.EqualTo(expected));
    }
}
