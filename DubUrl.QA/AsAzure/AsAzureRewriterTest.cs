using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Adomd.Wrappers;
using DubUrl.Adomd.Rewriting;
using DubUrl.Rewriting;

namespace DubUrl.QA.AsAzure;

[Category("AsAzure")]
[Category("ConnectionString")]
public class AsAzureRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.AnalysisServices.AdomdClient";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, AdomdFactory.Instance);

    [Test]
    [TestCase("westus/server", "asazure://westus.asazure.windows.net/server")]
    [TestCase("westus.asazure.windows.net/server", "asazure://westus.asazure.windows.net/server")]
    [TestCase("friendlyname.salesapp.azurewebsites.net", "link://friendlyname.salesapp.azurewebsites.net/")]
    public void Map_UrlInfo_DataSource(string input, string expected)
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new AsAzureRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SsasRewriter.SERVER_KEYWORD));
        Assert.That(result[SsasRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("westus")]
    [TestCase("westus.asazure.windows.net")]
    public void Map_InvalidUrlInfo_Throws(string input)
        => Assert.That(() =>
            new AsAzureRewriter(ConnectionStringBuilder)
                    .Execute(new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() })
            , Throws.TypeOf<InvalidConnectionUrlException>());

    [Test]
    [TestCase("westus/server/cube")]
    [TestCase("westus.asazure.windows.net/server/cube")]
    [TestCase("friendlyname.salesapp.azurewebsites.net/cube")]
    public void Map_UrlInfo_Cube(string input, string expected = "cube")
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new AsAzureRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(SsasRewriter.CUBE_KEYWORD));
        Assert.That(result[SsasRewriter.CUBE_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("westus/server/cube/any")]
    [TestCase("westus.asazure.windows.net/server/cube/any")]
    [TestCase("friendlyname.salesapp.azurewebsites.net/cube/any")]
    public void Map_InvalidSegments_Throws(string input)
        => Assert.That(() =>
            new AsAzureRewriter(ConnectionStringBuilder)
                    .Execute(new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() })
            , Throws.TypeOf<InvalidConnectionUrlException>());
}
