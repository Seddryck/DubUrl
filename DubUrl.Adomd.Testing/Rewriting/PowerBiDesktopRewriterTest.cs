using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Testing.Rewriting;
using DubUrl.Adomd.Wrappers;
using DubUrl.Adomd.Rewriting;
using DubUrl.Adomd.Discovery;
using Moq;
using DubUrl.Rewriting;

namespace DubUrl.Adomd.Testing.Rewriting.Implementation;

public class PowerBiDesktopRewriterTest
{
    private const string PROVIDER_NAME = "Microsoft.AnalysisServices.AdomdClient";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, AdomdFactory.Instance);
    }

    [Test]
    [TestCase("/myPowerBiFile")]
    [TestCase("127.0.0.1/myPowerBiFile")]
    [TestCase("./myPowerBiFile")]
    [TestCase("localhost/myPowerBiFile")]
    [TestCase("LocalHost/myPowerBiFile")]
    public void Map_UrlInfo_DataSource(string input)
    {
        var discoverer = new Mock<IPowerBiDiscoverer>();
        discoverer.Setup(x => x.GetPowerBiProcesses(false))
                                .Returns(new[] { new PowerBiProcess("myPowerBiFile", 12345, PowerBiType.PowerBI) });

        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder, discoverer.Object);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PowerBiDesktopRewriter.SERVER_KEYWORD));
        Assert.That(result[PowerBiDesktopRewriter.SERVER_KEYWORD], Is.EqualTo($"localhost:12345"));
    }

    [Test]
    [TestCase(":54321")]
    [TestCase("127.0.0.1:54321")]
    [TestCase(".:54321")]
    [TestCase("localhost:54321")]
    [TestCase("LocalHost:54321")]
    public void Map_UrlInfoWithPort_DataSource(string input)
    {
        var urlInfo = new UrlInfo() { Host = input.Split(':')[0], Port = Convert.ToInt32(input.Split(':')[1]), Segments= [] };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(PowerBiDesktopRewriter.SERVER_KEYWORD));
        Assert.That(result[PowerBiDesktopRewriter.SERVER_KEYWORD], Is.EqualTo($"localhost:54321"));
    }

    [TestCase("localhost:54321/foo")]
    public void Map_UrlInfoWithPortAndSegment_DataSource(string input)
    {
        var urlInfo = new UrlInfo() { Host = input.Split('/')[0].Split(':')[0], Port = Convert.ToInt32(input.Split('/')[0].Split(':')[1]), Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder);
        Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
    }

    [Test]
    [TestCase("foo/bar")]
    [TestCase("./foo?bar=brz")]
    public void Map_InvalidUrlInfo_DataSource(string input)
    {
        var discoverer = new Mock<IPowerBiDiscoverer>();
        discoverer.Setup(x => x.GetPowerBiProcesses(false))
                                .Returns(new[] { new PowerBiProcess("foo", 12345, PowerBiType.PowerBI) });

        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder, discoverer.Object);
        Assert.Throws<InvalidConnectionUrlException>(() => Rewriter.Execute(urlInfo));
    }

    [Test]
    [TestCase("./foo/bar")]
    public void Map_InvalidUrlInfo_TooManySegments(string input)
    {
        var discoverer = new Mock<IPowerBiDiscoverer>();
        discoverer.Setup(x => x.GetPowerBiProcesses(false))
                                .Returns(new[] { new PowerBiProcess("foo", 12345, PowerBiType.PowerBI) });

        var urlInfo = new UrlInfo() { Host = input.Split('/')[0], Segments = input.Split('/').Skip(1).ToArray() };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder, discoverer.Object);
        Assert.Throws<InvalidConnectionUrlTooManySegmentsException>(() => Rewriter.Execute(urlInfo));
    }

    [Test]
    public void Map_UrlInfo_DiscovererInteraction()
    {
        var discoverer = new Mock<IPowerBiDiscoverer>();
        discoverer.Setup(x => x.GetPowerBiProcesses(false))
                                .Returns(new[] { new PowerBiProcess("myPowerBiFile", 12345, PowerBiType.PowerBI) });

        var urlInfo = new UrlInfo() { Host = "localhost", Segments = ["myPowerBiFile"] };
        var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder, discoverer.Object);
        var result = Rewriter.Execute(urlInfo);

        discoverer.Verify(x => x.GetPowerBiProcesses(false), Times.Once());
    }
}
