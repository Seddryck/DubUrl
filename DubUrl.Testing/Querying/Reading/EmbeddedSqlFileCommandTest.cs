using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Reading.ResourceManagement;
using DubUrl.Querying.Reading.ResourceMatching;
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
    public class EmbeddedSqlFileCommandTest
    {
        [Test]
        public void Locate_ResourceMatcherFactoryInstantiate()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.ListResources()).Returns(new string[] { "QueryId.sql" });

            var resourceMatcher = new Mock<IResourceMatcher>();
            resourceMatcher.Setup(x=> x.Execute(It.IsAny<string>(), It.IsAny<string[]>())).Returns("QueryId.sql");

            var resourceMatcherFactory = new Mock<IResourceMatcherFactory>();
            resourceMatcherFactory.Setup(x => x.Instantiate(It.IsAny<IConnectivity>(), It.IsAny<string[]>())).Returns(resourceMatcher.Object);

            var query = new EmbeddedSqlFileCommand(resourceManager.Object, resourceMatcherFactory.Object, "QueryId");
            var connectivity = new NativeConnectivity();
            var dialect = new MssqlDialect(new[] { "mssql" });

            var result = query.Locate(connectivity, dialect);

            resourceMatcherFactory.Verify(x => x.Instantiate(connectivity, dialect.Aliases));
            resourceMatcher.Verify(x => x.Execute("QueryId", new string[] { "QueryId.sql" }));
        }

        [Test]
        public void Locate_ListResourcesCalled()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.ListResources()).Returns(new string[] { "QueryId.sql" });

            var resourceMatcher = new Mock<IResourceMatcher>();
            resourceMatcher.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string[]>())).Returns("QueryId.sql");

            var resourceMatcherFactory = new Mock<IResourceMatcherFactory>();
            resourceMatcherFactory.Setup(x => x.Instantiate(It.IsAny<IConnectivity>(), It.IsAny<string[]>())).Returns(resourceMatcher.Object);

            var query = new EmbeddedSqlFileCommand(resourceManager.Object, resourceMatcherFactory.Object, "QueryId");
            var connectivity = new NativeConnectivity();
            var dialect = new MssqlDialect(new[] { "mssql" });

            var result = query.Locate(connectivity, dialect);

            resourceManager.Verify(x => x.ListResources());
        }

        [Test]
        public void Locate_ResourceMatcherCalled()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.ListResources()).Returns(new string[] { "QueryId.sql" });

            var resourceMatcher = new Mock<IResourceMatcher>();
            resourceMatcher.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string[]>())).Returns("QueryId.sql");

            var resourceMatcherFactory = new Mock<IResourceMatcherFactory>();
            resourceMatcherFactory.Setup(x => x.Instantiate(It.IsAny<IConnectivity>(), It.IsAny<string[]>())).Returns(resourceMatcher.Object);

            var query = new EmbeddedSqlFileCommand(resourceManager.Object, resourceMatcherFactory.Object, "QueryId");
            var connectivity = new NativeConnectivity();
            var dialect = new MssqlDialect(new[] { "mssql" });

            var result = query.Locate(connectivity, dialect);

            resourceMatcher.Verify(x => x.Execute("QueryId", new string[] { "QueryId.sql" }));
        }

        [Test]
        public void Read_ReadCommandTextCalled()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.ListResources()).Returns(new string[] { "QueryId.sql" });

            var resourceMatcher = new Mock<IResourceMatcher>();
            resourceMatcher.Setup(x => x.Execute(It.IsAny<string>(), It.IsAny<string[]>())).Returns("QueryId.sql");

            var resourceMatcherFactory = new Mock<IResourceMatcherFactory>();
            resourceMatcherFactory.Setup(x => x.Instantiate(It.IsAny<IConnectivity>(), It.IsAny<string[]>())).Returns(resourceMatcher.Object);

            var query = new EmbeddedSqlFileCommand(resourceManager.Object, resourceMatcherFactory.Object, "QueryId");
            var connectivity = new NativeConnectivity();
            var dialect = new MssqlDialect(new[] { "mssql" });

            var result = query.Read(dialect, connectivity);

            resourceManager.Verify(x => x.ReadCommandText("QueryId.sql"));
        }
    }
}
