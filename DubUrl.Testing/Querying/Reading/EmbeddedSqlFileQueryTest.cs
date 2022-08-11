using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading
{
    public class EmbeddedSqlFileQueryTest
    {
        private class FakeEmbeddedSqlFileResourceManager : EmbeddedSqlFileResourceManager
        {
            private string[] resourceNames;
            public override string[] ResourceNames { get => resourceNames; }
            public FakeEmbeddedSqlFileResourceManager(string[] resourceNames)
                : base(Assembly.GetCallingAssembly())
                => this.resourceNames = resourceNames;
        }

        [Test]
        [TestCase(true, "QueryId.sql", "QueryId", new[] { "mssql" })]
        [TestCase(true, "QueryId.mssql.sql", "QueryId", new[] { "mssql" })]
        [TestCase(false, "", "QueryId", new[] { "mssql" }, false)]
        public void ExistsWithFallback_ListOfResources_Value(bool hasCandidates, string bestCandidate, string id, string[] dialects, bool expected = true)
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup( x=> x.Any(id, dialects)).Returns(hasCandidates);
            resourceManager.Setup(x => x.BestMatch(id, dialects)).Returns(bestCandidate);

            var query = new EmbeddedSqlFileQuery(id, resourceManager.Object);
            var result = query.Exists(dialects, true);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        [TestCase(true, "QueryId.sql", "QueryId", new[] { "mssql" }, false)]
        [TestCase(true, "QueryId.mssql.sql", "QueryId", new[] { "mssql" })]
        [TestCase(false, "", "QueryId", new[] { "mssql" }, false)]
        public void ExistsWithoutFallback_ListOfResources_Value(bool hasCandidates, string bestCandidate, string id, string[] dialects, bool expected = true)
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.Any(id, dialects)).Returns(hasCandidates);
            resourceManager.Setup(x => x.BestMatch(id, dialects)).Returns(bestCandidate);

            var query = new EmbeddedSqlFileQuery(id, resourceManager.Object);
            var result = query.Exists(dialects, false);
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
