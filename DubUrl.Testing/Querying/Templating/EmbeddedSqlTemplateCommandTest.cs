using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Templating;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading
{
    public class EmbeddedSqlTemplateCommandTest
    {
        private class FakeEmbeddedTemplateFileResourceManager : EmbeddedSqlTemplateResourceManager
        {
            private readonly string[] resourceNames;
            public override string[] ResourceNames { get => resourceNames; }
            public FakeEmbeddedTemplateFileResourceManager(string[] resourceNames)
                : base(Assembly.GetCallingAssembly())
                => this.resourceNames = resourceNames;
        }

        [Test]
        public void Read_ListOfResources_ResourceManagerCallingReadResourceForEachTemplate()
        {
            var dialect = Mock.Of<IDialect>(d => d.Aliases == new[] { "duck", "duckdb" });
            var connectivity = Mock.Of<IConnectivity>(c => c.Alias == string.Empty);

            var resourceManager = new Mock<IResourceTemplateManager>();
            resourceManager.Setup(x => x.Any("queryId", dialect.Aliases, connectivity.Alias)).Returns(true);
            resourceManager.Setup(x => x.BestMatch("queryId", dialect.Aliases, connectivity.Alias)).Returns("queryId.duckdb.sql.st");
            resourceManager.Setup(x => x.ReadResource("queryId.duckdb.sql.st")).Returns("Hi $print_name()$");
            resourceManager.Setup(x => x.ListResources("Foo.Bar", dialect.Aliases, connectivity.Alias, "sql.st"))
                                .Returns(new Dictionary<string, string>() { { "print_name", "Foo.Bar.print_name.sql.st" }, { "print_end", "Foo.Bar.DuckDB.print_end.sql.st" } });
            resourceManager.Setup(x => x.ListResources("Foo.Bar", dialect.Aliases, connectivity.Alias, "dic.st"))
                                .Returns(new Dictionary<string, string>());
            resourceManager.Setup(x => x.ReadResource("Foo.Bar.print_name.sql.st")).Returns("$name$$print_end()$");
            resourceManager.Setup(x => x.ReadResource("Foo.Bar.DuckDB.print_end.sql.st")).Returns("!");

            var query = new EmbeddedSqlTemplateCommand(resourceManager.Object, "queryId", "Foo.Bar", "Foo.Bar", new Dictionary<string, object?>() { { "name", "Cédric" } }, NullQueryLogger.Instance);

            var response = query.Read(dialect, connectivity);
            Assert.That(response, Is.EqualTo("Hi Cédric!"));

            resourceManager.VerifyAll();
        }

        [Test]
        public void Read_AnyExistingResources_InvokeLog()
        {
            var dialect = Mock.Of<IDialect>(d => d.Aliases == new[] { "duck", "duckdb" });
            var connectivity = Mock.Of<IConnectivity>(c => c.Alias == string.Empty);

            var resourceManager = new Mock<IResourceTemplateManager>();
            resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<string?>())).Returns(true);
            resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<string[]>(), string.Empty)).Returns("foo");
            resourceManager.Setup(x => x.ListResources(It.IsAny<string>(), dialect.Aliases, connectivity.Alias, It.IsAny<string>())).Returns(new Dictionary<string, string>());
            resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("bar");

            var queryLoggerMock = new Mock<IQueryLogger>();

            var query = new EmbeddedSqlTemplateCommand(resourceManager.Object, "queryId", "Foo.Bar", "Foo.Bar", new Dictionary<string, object?>() { { "name", "Cédric" } }, queryLoggerMock.Object);
            var result = query.Read(dialect, connectivity);

            queryLoggerMock.Verify(log => log.Log(It.IsAny<string>()));
        }
    }
}
