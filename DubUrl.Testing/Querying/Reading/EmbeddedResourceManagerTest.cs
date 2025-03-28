using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Reading;
using DubUrl.Querying.TypeMapping;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading;

public class EmbeddedResourceManagerTest
{
    private class FakeEmbeddedSqlFileResourceManager : EmbeddedResourceManager
    {
        private readonly string[] resourceNames;
        public override string[] ResourceNames { get => resourceNames; }
        public FakeEmbeddedSqlFileResourceManager(string[] resourceNames)
            : base(Assembly.GetCallingAssembly())
            => this.resourceNames = resourceNames;
    }

    [Test]
    [TestCase(new[] { "QueryId.sql", "OtherQueryId.sql" }, "QueryId", new[] { "mssql" }, 0)]
    [TestCase(new[] { "OtherQueryId.sql", "QueryId.sql" }, "QueryId", new[] { "mssql" }, 1)]
    [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
    [TestCase(new[] { "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 2)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "ms", "mssql" }, 2)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql", "QueryId.common.sql" }, "QueryId", new[] { "ms", "mssql" }, 2)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mysql" }, 0)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql", "QueryId.common.sql" }, "QueryId", new[] { "mysql" }, 3)]
    public void BestMatch_ListOfResources_BestMatch(string[] candidates, string id, string[] dialects, int expectedId)
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == dialects && x.Language == new SqlLanguage());
        var resourceManager = new FakeEmbeddedSqlFileResourceManager(candidates);
        var resourceName = resourceManager.BestMatch(id, dialect, null);
        Assert.That(resourceName, Is.EqualTo(candidates[expectedId]).IgnoreCase);
    }

    [Test]
    [TestCase(new[] { "QueryId.sql", "OtherQueryId.sql" }, true)]
    [TestCase(new[] { "OtherQueryId.sql", "QueryId.sql" }, true)]
    [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, true)]
    [TestCase(new[] { "QueryId.pgsql.sql", "QueryId.mssql.sql" }, true)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, true)]
    [TestCase(new[] { "OtherQueryId.sql", "OtherQueryId.pgsql.sql", "UnexpectedQueryId.mssql.sql" }, false)]
    public void GetAllResourceNames_ListOfResources_BestMatch(string[] candidates, bool expected = true)
    {
        var resourceManager = new FakeEmbeddedSqlFileResourceManager(candidates);
        var resourceName = resourceManager.Any("QueryId", new TSqlDialect(new SqlLanguage(), ["mssql"], new AnsiRenderer(), [], TSqlTypeMapper.Instance, TSqlFunctionMapper.Instance), null);
        Assert.That(resourceName, Is.EqualTo(expected));
    }

    [Test]
    [TestCase(new[] { "QueryId.odbc.mssql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, "odbc", 0)]
    [TestCase(new[] { "QueryId.odbc.mssql.sql", "QueryId.odbc.sql" }, "QueryId", new[] { "mssql" }, "odbc", 0)]
    [TestCase(new[] { "QueryId.odbc.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, "odbc", 0)]
    [TestCase(new[] { "QueryId.odbc.common.sql", "QueryId.pgsql.sql" }, "QueryId", new[] { "mssql" }, "odbc", 0)]
    [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql" }, "QueryId", new[] { "mssql" }, "odbc", 0)]
    public void BestMatch_ListOfResourcesWithOdbc_BestMatch(string[] candidates, string id, string[] dialects, string? connectivity, int expectedId)
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == dialects && x.Language==new SqlLanguage());
        var resourceManager = new FakeEmbeddedSqlFileResourceManager(candidates);
        var resourceName = resourceManager.BestMatch(id, dialect, connectivity);
        Assert.That(resourceName, Is.EqualTo(candidates[expectedId]).IgnoreCase);
    }
}
