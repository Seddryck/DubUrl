using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Prql.Testing
{
    
    public class EmbeddedPrqlFileResourceManagerTest
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
        [TestCase(new[] { "QueryId.prql", "OtherQueryId.prql" }, "QueryId", 0)]
        [TestCase(new[] { "OtherQueryId.prql", "QueryId.prql" }, "QueryId", 1)]
        public void BestMatch_ListOfResources_BestMatch(string[] candidates, string id, int expectedId)
        {
            var resourceManager = new FakeEmbeddedPrqlFileResourceManager(candidates);
            var resourceName = resourceManager.BestMatch(id, new NoneMatchingOption());
            Assert.That(resourceName, Is.EqualTo(candidates[expectedId]).IgnoreCase);
        }

        [Test]
        [TestCase(new[] { "QueryId.prql", "OtherQueryId.prql" }, true)]
        [TestCase(new[] { "OtherQueryId.prql", "UnexpectedQueryId.prql" }, false)]
        public void GetAllResourceNames_ListOfResources_BestMatch(string[] candidates, bool expected = true)
        {
            var resourceManager = new FakeEmbeddedPrqlFileResourceManager(candidates);
            var resourceName = resourceManager.Any("QueryId", new NoneMatchingOption());
            Assert.That(resourceName, Is.EqualTo(expected));
        }
    }
}
