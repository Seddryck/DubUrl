using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying
{
    public class CommandFactoryTest
    {
        [Test]
        public void Execute_ExistingQuery_CommandCorrectlySetuped()
        {
            var readerMock = new Mock<ICommandReader>();
            readerMock.Setup(x => x.GetAllResourceNames()).Returns(new[] { "MyAssembly.MyNamespace.QueryId.sql", "MyAssembly.MyNamespace.OtherQueryId.sql" });
            readerMock.Setup(x => x.ReadCommandText(It.IsAny<string>())).Returns("select * from [table]");
            readerMock.Setup(x => x.ReadParameters(It.IsAny<string>())).Returns(Array.Empty<DubUrl.Querying.ParameterInfo>());

            var cmdMock = new Mock<IDbCommand>();
            cmdMock.SetupSet(x => x.CommandType = CommandType.Text);
            cmdMock.SetupSet(x => x.CommandText = "select * from [table]");
            
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory(readerMock.Object);
            var cmd = factory.Execute(connMock.Object, "MyAssembly.MyNamespace.QueryId", new[] {"mssql" });
            Assert.IsNotNull(cmd);
            cmdMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_CommandFromConnection()
        {
            var readerMock = new Mock<ICommandReader>();
            readerMock.Setup(x => x.GetAllResourceNames()).Returns(new[] { "MyAssembly.MyNamespace.QueryId.sql", "MyAssembly.MyNamespace.OtherQueryId.sql" });
            readerMock.Setup(x => x.ReadCommandText(It.IsAny<string>())).Returns("select * from [table]");
            readerMock.Setup(x => x.ReadParameters(It.IsAny<string>())).Returns(Array.Empty<DubUrl.Querying.ParameterInfo>());

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory(readerMock.Object);
            var cmd = factory.Execute(connMock.Object, "MyAssembly.MyNamespace.QueryId", new[] { "mssql" });
            Assert.IsNotNull(cmd);
            connMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_ReaderCorrectlyCalled()
        {
            var readerMock = new Mock<ICommandReader>();
            readerMock.Setup(x => x.GetAllResourceNames()).Returns(new[] { "MyAssembly.MyNamespace.QueryId.sql", "MyAssembly.MyNamespace.OtherQueryId.sql" });
            readerMock.Setup(x => x.ReadCommandText(It.IsAny<string>())).Returns("select * from [table]");
            readerMock.Setup(x => x.ReadParameters(It.IsAny<string>())).Returns(Array.Empty<DubUrl.Querying.ParameterInfo>());

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory(readerMock.Object);
            var cmd = factory.Execute(connMock.Object, "MyAssembly.MyNamespace.QueryId", new[] { "mssql" });
            readerMock.Verify(x => x.GetAllResourceNames(), Times.Once);
            readerMock.Verify(x => x.ReadCommandText("MyAssembly.MyNamespace.QueryId.sql"), Times.Once);
            readerMock.Verify(x => x.ReadParameters(It.IsAny<string>()), Times.Once);
        }

        [Test]
        [TestCase(new[] { "QueryId.sql", "OtherQueryId.sql" }, "QueryId", new[] { "mssql" }, 0)]
        [TestCase(new[] { "OtherQueryId.sql", "QueryId.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 1)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mssql" }, 2)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "ms", "mssql" }, 2)]
        [TestCase(new[] { "QueryId.sql", "QueryId.pgsql.sql", "QueryId.mssql.sql" }, "QueryId", new[] { "mysql" }, 0)]
        public void RetrieveBestMatch_ListOfResources_BestMatch(string[] candidates, string id, string[] dialects, int expectedId)
        {
            var readerMock = new Mock<ICommandReader>();
            readerMock.Setup(x => x.GetAllResourceNames()).Returns(candidates);

            var factory = new CommandFactory(readerMock.Object);
            var resourceName = factory.RetrieveBestMatch(id, dialects);
            readerMock.Verify(x => x.GetAllResourceNames(), Times.Once);
            Assert.That(resourceName, Is.EqualTo(candidates[expectedId]));
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
            var readerMock = new Mock<ICommandReader>();
            readerMock.Setup(x => x.GetAllResourceNames()).Returns(candidates);

            var factory = new CommandFactory(readerMock.Object);
            var resourceName = factory.IsExistingQuery("QueryId");
            readerMock.Verify(x => x.GetAllResourceNames(), Times.Once);
            Assert.That(resourceName, Is.EqualTo(expected));
        }
    }
}
