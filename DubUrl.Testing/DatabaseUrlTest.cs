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
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System.Linq.Expressions;

namespace DubUrl.Testing
{
    public class DatabaseUrlTest
    {
        private static Mock<IDbCommand> DefineExpr(
            Expression<Func<IDbCommand, object?>> expression
            , object? returnValue = null
        )
        {
            var commandMock = new Mock<IDbCommand>();
            if (returnValue==null)
                commandMock.Setup(expression);
            else
                commandMock.Setup(expression).Returns(returnValue);
            return commandMock;
        }

        static readonly object[] Actions =
        {
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ExecuteReader("QueryId"))
                , DefineExpr(x => x.ExecuteReader()) },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadScalar("QueryId"))
                , DefineExpr(x => x.ExecuteScalar(), "Value") },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadScalarNonNull("QueryId"))
                , DefineExpr(x => x.ExecuteScalar(), "Value") }
        };

        
        [TestCaseSource(nameof(Actions))]
        public void DbAction_AnyQuery_MocksVerified(Action<DatabaseUrl> dbAction, Mock<IDbCommand> commandMock)
        {
            var connectionMock = new Mock<IDbConnection>();
            connectionMock.Setup(x => x.CreateCommand()).Returns(commandMock.Object);

            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Open()).Returns(connectionMock.Object);

            var cpfMock = new Mock<CommandProvisionerFactory>();
            cpfMock.Setup(x => x.Instantiate(It.IsAny<ICommandProvider>(), It.IsAny<ConnectionUrl>()))
                .Returns(Array.Empty<ICommandProvisioner>());

            var db = new DatabaseUrl(connectionUrlMock.Object, cpfMock.Object);
            dbAction.Invoke(db);

            connectionUrlMock.VerifyAll();
            connectionMock.VerifyAll();
            commandMock.VerifyAll();
            cpfMock.VerifyAll();
        }
    }
}
