using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using DubUrl.Testing.Rewriting;
using DubUrl.Adomd.Wrappers;
using DubUrl.Adomd.Rewriting;
using DubUrl.Adomd.Discovery;
using Moq;

namespace DubUrl.Adomd.Testing.Rewriting.Implementation
{
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
        public void Map_UrlInfo_DiscovererInteraction()
        {
            var discoverer = new Mock<IPowerBiDiscoverer>();
            discoverer.Setup(x => x.GetPowerBiProcesses(false))
                                    .Returns(new[] { new PowerBiProcess("myPowerBiFile", 12345, PowerBiType.PowerBI) });

            var urlInfo = new UrlInfo() { Host = "localhost", Segments = new[] { "myPowerBiFile" } };
            var Rewriter = new PowerBiDesktopRewriter(ConnectionStringBuilder, discoverer.Object);
            var result = Rewriter.Execute(urlInfo);

            discoverer.Verify(x => x.GetPowerBiProcesses(false), Times.Once());
        }
    }
}
