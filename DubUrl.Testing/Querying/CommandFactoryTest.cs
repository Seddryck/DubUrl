using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
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
            var queryMock = new Mock<IQuery>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            cmdMock.SetupSet(x => x.CommandType = CommandType.Text);
            cmdMock.SetupSet(x => x.CommandText = "select * from [table]");
            
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory();
            var cmd = factory.Execute(connMock.Object, queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            Assert.IsNotNull(cmd);
            cmdMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_CommandFromConnection()
        {
            var queryMock = new Mock<IQuery>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory();
            var cmd = factory.Execute(connMock.Object, queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            Assert.IsNotNull(cmd);
            connMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_QueryRead()
        {
            var queryMock = new Mock<IQuery>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var factory = new CommandFactory();
            var cmd = factory.Execute(connMock.Object, queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            Assert.IsNotNull(cmd);
            queryMock.VerifyAll();
        }
    }
}
