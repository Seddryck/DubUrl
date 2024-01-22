using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Testing.Rewriting;
using DubUrl.Adomd.Wrappers;
using DubUrl.Adomd.Rewriting;
using DubUrl.Rewriting;

namespace DubUrl.Adomd.Testing.Rewriting.Implementation;

public class PowerBiPremiumRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.AnalysisServices.AdomdClient";

    private static DbConnectionStringBuilder ConnectionStringBuilder
        => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, AdomdFactory.Instance);

    [Test]
    [TestCase("/myWorkspace")]
    [TestCase("/my Workspace", "api.powerbi.com/v1.0/myorg/my%20Workspace")]
    [TestCase("/my%20Workspace", "api.powerbi.com/v1.0/myorg/my%20Workspace")]
    [TestCase("/myOrganization/myWorkspace", "api.powerbi.com/v1.0/myOrganization/myWorkspace")]
    [TestCase("/v1.0/myorg/myWorkspace")]
    [TestCase("api.powerbi.com/myWorkspace")]
    [TestCase("api.powerbi.com/myorg/myWorkspace")]
    [TestCase("api.powerbi.com/v1.0/myorg/myWorkspace")]
    [TestCase("api.powerbi.com/v1.0/myOrganization/myWorkspace", "api.powerbi.com/v1.0/myOrganization/myWorkspace")]
    public void Map_UrlInfo_DataSource(string input, string expected = "api.powerbi.com/v1.0/myorg/myWorkspace")
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new PowerBiPremiumRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PowerBiPremiumRewriter.SERVER_KEYWORD));
        Assert.That(result[PowerBiPremiumRewriter.SERVER_KEYWORD], Is.EqualTo($"powerbi://{expected}"));
    }

    [Test]
    [TestCase("api.foo.com/v1.0/myorg/myWorkspace")]
    [TestCase("api.powerbi.com/v1.0/myorg/myWorkspace/foo")]
    [TestCase("api.powerbi.com")]
    [TestCase("api.powerbi.com/")]
    public void Map_InvalidUrlInfo_DataSource(string input)
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray() };
        var Rewriter = new PowerBiPremiumRewriter(ConnectionStringBuilder);

        Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
    }
}
