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

namespace DubUrl.Testing.Querying.Reading;

public class EmbeddedResourceCommandTest
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
    [TestCase(true, "QueryId.sql", "QueryId", new[] { "mssql" })]
    [TestCase(true, "QueryId.mssql.sql", "QueryId", new[] { "mssql" })]
    [TestCase(false, "", "QueryId", new[] { "mssql" }, false)]
    public void ExistsWithFallback_ListOfResources_Value(bool hasCandidates, string bestCandidate, string id, string[] dialects, bool expected = true)
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == dialects && x.Language == new SqlLanguage());
        var connectivity = Mock.Of<IConnectivity>(x => x.Alias == string.Empty);

        var resourceManager = new Mock<IResourceManager>();
        resourceManager.Setup( x=> x.Any(id, dialect, string.Empty)).Returns(hasCandidates);
        resourceManager.Setup(x => x.BestMatch(id, dialect, string.Empty)).Returns(bestCandidate);

        var query = new EmbeddedResourceCommand(resourceManager.Object, id, NullQueryLogger.Instance);
        var result = query.Exists(dialect, connectivity, true);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("QueryId.sql", "QueryId", new[] { "mssql" }, false)]
    [TestCase("QueryId.mssql.sql", "QueryId", new[] { "mssql" })]
    [TestCase("", "QueryId", new[] { "mssql" }, false)]
    public void ExistsWithoutFallback_ListOfResources_Value(string bestCandidate, string id, string[] dialects, bool expected = true)
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == dialects && x.Language == new SqlLanguage());
        var connectivity = Mock.Of<IConnectivity>(x => x.Alias == string.Empty);

        var resourceManager = new Mock<IResourceManager>();
        resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<IDialect>(), It.IsAny<string?>())).Returns(!string.IsNullOrEmpty(bestCandidate));
        resourceManager.Setup(x => x.BestMatch(id, dialect, string.Empty)).Returns(bestCandidate);

        var query = new EmbeddedResourceCommand(resourceManager.Object, id, NullQueryLogger.Instance);
        var result = query.Exists(dialect, connectivity, false);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Read_Existing_BestMatchIsRead()
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == new[] { "mssql" } && x.Language == new SqlLanguage());
        var connectivity = Mock.Of<IConnectivity>(x => x.Alias == string.Empty);

        var resourceManager = new Mock<IResourceManager>();
        resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<IDialect>(), It.IsAny<string?>())).Returns(true);
        resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<IDialect>(), string.Empty)).Returns("foo");
        resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("bar");

        var query = new EmbeddedResourceCommand(resourceManager.Object, "foo", NullQueryLogger.Instance);
        var result = query.Read(dialect, connectivity);

        resourceManager.Verify(x => x.ReadResource("foo"));
    }

    [Test]
    public void Read_AnyExistingResources_InvokeLog()
    {
        var dialect = Mock.Of<IDialect>(x => x.Aliases == new[] { "mssql" } && x.Language == new SqlLanguage());
        var connectivity = Mock.Of<IConnectivity>(x => x.Alias == string.Empty);

        var resourceManager = new Mock<IResourceManager>();
        resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<IDialect>(), It.IsAny<string?>())).Returns(true);
        resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<IDialect>(), string.Empty)).Returns("foo");
        resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("bar");

        var queryLoggerMock = new Mock<IQueryLogger>();

        var query = new EmbeddedResourceCommand(resourceManager.Object, "foo", queryLoggerMock.Object);
        var result = query.Read(dialect, connectivity);

        queryLoggerMock.Verify(log => log.Log(It.IsAny<string>()));
    }
}
