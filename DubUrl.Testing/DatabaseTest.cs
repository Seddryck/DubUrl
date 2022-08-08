using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;

namespace DubUrl.Testing
{
    public class DatabaseTest
    {

        [Test]
        public void Connect_AnyConnectionString_OneCallToConnectionUrl()
        {
            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Connect());

            var commandFactoryMock = new Mock<CommandFactory>(It.IsAny<ICommandReader>());

            var db = new Database(connectionUrlMock.Object, commandFactoryMock.Object);
            db.Connect();

            connectionUrlMock.VerifyAll();
        }

        [Test]
        public void Open_AnyConnectionString_OneCallToConnectionUrl()
        {
            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Open());

            var commandFactoryMock = new Mock<CommandFactory>(It.IsAny<ICommandReader>());

            var db = new Database(connectionUrlMock.Object, commandFactoryMock.Object);
            db.Open();

            connectionUrlMock.VerifyAll();
        }

        [Test]
        public void ExecuteScalar_AnyQuery_OneCallToCommand()
        {
            var connectionStub = new Mock<IDbConnection>();
            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(x => x.ExecuteScalar());

            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Open()).Returns(connectionStub.Object);
            connectionUrlMock.SetupGet(x => x.Dialects).Returns(new[] { "mssql" });

            var commandFactoryMock = new Mock<CommandFactory>(It.IsAny<ICommandReader>());
            commandFactoryMock.Setup(x => x.Execute(It.IsAny<IDbConnection>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns(commandMock.Object);

            var db = new Database(connectionUrlMock.Object, commandFactoryMock.Object);
            db.ExecuteScalar("QueryId");

            connectionUrlMock.VerifyAll();
        }

        [Test]
        public void ExecuteScalar_AnyQuery_OneCallToCommandExecuteScalar()
        {
            var connectionStub = new Mock<IDbConnection>();
            var commandMock = new Mock<IDbCommand>();
            commandMock.Setup(x => x.ExecuteScalar());

            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Open()).Returns(connectionStub.Object);
            connectionUrlMock.SetupGet(x => x.Dialects).Returns(new[] { "mssql" });

            var commandFactoryMock = new Mock<CommandFactory>(It.IsAny<ICommandReader>());
            commandFactoryMock.Setup(x => x.Execute(It.IsAny<IDbConnection>(), It.IsAny<string>(), It.IsAny<string[]>()))
                .Returns(commandMock.Object);

            var db = new Database(connectionUrlMock.Object, commandFactoryMock.Object);
            db.ExecuteScalar("QueryId");

            commandMock.VerifyAll();
        }
    }
}
