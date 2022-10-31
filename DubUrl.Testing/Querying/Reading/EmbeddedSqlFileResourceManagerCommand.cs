using DubUrl.Querying.Reading.ResourceManagement;
using DubUrl.Querying.Reading.ResourceMatching;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading.ResourceMatching
{

    public class AdoNetResourceMatcherTest
    {
        [Test]
        [TestCase(new[] { "QueryId.sql", "OtherQueryId.sql" }, "QueryId", new[] { "mssql" }, 0)]
        [TestCase(new[] { "OtherQueryId.sql", "QueryId.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 2)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "ms", "mssql" }, 2)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mysql" }, 0)]
        public void Execute_ListOfResources_Matching(string[] candidates, string id, string[] dialects, int expectedId)
        {
            var matcher = new AdoNetResourceMatcher(dialects);
            var match = matcher.Execute(id, candidates);
            Assert.That(match, Is.EqualTo(candidates[expectedId]));
        }
    }
}
