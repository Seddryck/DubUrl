using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Prql.Testing
{
    public class EmbeddedPrqlFileProviderTest
    {
        private class FakeEmbeddedPrqlFileResourceManager : EmbeddedPrqlFileResourceManager
        {
            private readonly string[] resourceNames;
            public override string[] ResourceNames { get => resourceNames; }
            public FakeEmbeddedPrqlFileResourceManager(string[] resourceNames)
                : base(Assembly.GetCallingAssembly())
                => this.resourceNames = resourceNames;
        }

        [Test]
        [TestCase(true, "QueryId.prql", "QueryId")]
        [TestCase(false, "", "QueryId", false)]
        public void ExistsWithFallback_ListOfResources_Value(bool hasCandidates, string bestCandidate, string id, bool expected = true)
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup( x=> x.Any(id, It.IsAny<NoneMatchingOption>())).Returns(hasCandidates);
            resourceManager.Setup(x => x.BestMatch(id, It.IsAny<NoneMatchingOption>())).Returns(bestCandidate);

            var dialectMock = new Mock<IDialect>();
            dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            var connectivityMock = new Mock<IConnectivity>();
            connectivityMock.SetupGet(x => x.Alias).Returns(string.Empty);

            var query = new EmbeddedPrqlFileProvider(resourceManager.Object, id, NullQueryLogger.Instance);
            var result = query.Exists(dialectMock.Object, connectivityMock.Object, true);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void Read_Existing_BestMatchIsRead()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<NoneMatchingOption>())).Returns(true);
            resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<NoneMatchingOption>())).Returns("foo");
            resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("select 'bar'");

            var dialectMock = new Mock<IDialect>();
            dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            var connectivityMock = new Mock<IConnectivity>();
            connectivityMock.SetupGet(x => x.Alias).Returns(string.Empty);

            var query = new EmbeddedPrqlFileProvider(resourceManager.Object, "foo", NullQueryLogger.Instance);
            var result = query.Read(dialectMock.Object, connectivityMock.Object);

            resourceManager.Verify(x => x.ReadResource("foo"));
        }

        [Test]
        public void Read_AnyExistingResources_InvokeLog()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<NoneMatchingOption>())).Returns(true);
            resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<NoneMatchingOption>())).Returns("foo");
            resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("select 'bar'");

            var dialectMock = new Mock<IDialect>();
            dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            var connectivityMock = new Mock<IConnectivity>();
            connectivityMock.SetupGet(x => x.Alias).Returns(string.Empty);

            var queryLoggerMock = new Mock<IQueryLogger>();

            var query = new EmbeddedPrqlFileProvider(resourceManager.Object, "foo", queryLoggerMock.Object);
            var result = query.Read(dialectMock.Object, connectivityMock.Object);

            queryLoggerMock.Verify(log => log.Log(It.IsAny<string>()));
        }
    }
}
