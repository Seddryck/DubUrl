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
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System.Linq.Expressions;
using DubUrl.MicroOrm;

namespace DubUrl.Testing
{
    public abstract class DatabaseUrlUtilities
    {
        protected static Mock<IDbCommand> DefineExpr(
            Expression<Func<IDbCommand, object?>> expression
            , object? returnValue = null
        )
        {
            var commandMock = new Mock<IDbCommand>();
            if (returnValue == null)
                commandMock.Setup(expression);
            else
                commandMock.Setup(expression).Returns(returnValue);
            return commandMock;
        }

        

        protected static Mock<IDataReader> DefineDataReader()
        {
            var dataReaderMock = new Mock<IDataReader>();
            dataReaderMock.SetupGet(x => x.FieldCount).Returns(3);
            dataReaderMock.SetupSequence(x => x.Read()).Returns(true).Returns(false);
            dataReaderMock.SetupSequence(x => x.GetName(It.IsAny<int>())).Returns("CustomerId").Returns("FullName").Returns("BirthDate");
            dataReaderMock.SetupSequence(x => x.GetValue(It.IsAny<int>())).Returns(5).Returns("Linus Torvalds").Returns(new DateTime(1969, 12, 28));
            return dataReaderMock;
        }

        protected virtual DatabaseUrl CreateDbUrl(ConnectionUrl connectionUrl, CommandProvisionerFactory cpf)
            => new(connectionUrl, cpf, NullQueryLogger.Instance);

        protected static Mock<IDataReader> DefineTwoRows()
        {
            var dataReaderMock = new Mock<IDataReader>();
            dataReaderMock.SetupGet(x => x.FieldCount).Returns(3);
            dataReaderMock.SetupSequence(x => x.Read()).Returns(true).Returns(true).Returns(false);
            dataReaderMock.SetupSequence(x => x.GetName(It.IsAny<int>()))
                .Returns("CustomerId").Returns("FullName").Returns("BirthDate")
                .Returns("CustomerId").Returns("FullName").Returns("BirthDate");
            dataReaderMock.SetupSequence(x => x.GetValue(It.IsAny<int>()))
                .Returns(5).Returns("Linus Torvalds").Returns(new DateTime(1969, 12, 28))
                .Returns(4).Returns("Alan Turing").Returns(new DateTime(1912, 6, 23));
            dataReaderMock.SetupSequence(x => x["CustomerId"])
                .Returns(5).Returns(4);
            dataReaderMock.SetupSequence(x => x["FullName"])
                .Returns("Linus Torvalds").Returns("Alan Turing");
            dataReaderMock.SetupSequence(x => x["BirthDate"])
                .Returns(new DateTime(1969, 12, 28)).Returns(new DateTime(1912, 6, 23));

            return dataReaderMock;
        }

        protected virtual DatabaseUrl GetDatabaseUrl(Mock<IDataReader> dataReaderMock)
        {
            var commandMock = DefineExpr(x => x.ExecuteReader(), dataReaderMock.Object);

            var connectionMock = new Mock<IDbConnection>();
            connectionMock.Setup(x => x.CreateCommand()).Returns(commandMock.Object);

            var connectionUrlMock = new Mock<ConnectionUrl>(It.IsAny<string>());
            connectionUrlMock.Setup(x => x.Open()).Returns(connectionMock.Object);

            var cpfMock = new Mock<CommandProvisionerFactory>();
            cpfMock.Setup(x => x.Instantiate(It.IsAny<ICommandProvider>(), It.IsAny<ConnectionUrl>()))
                .Returns(Array.Empty<ICommandProvisioner>());

            return CreateDbUrl(connectionUrlMock.Object, cpfMock.Object);
        }

        protected static Mock<IDataReader> DefineZeroRows()
        {
            var dataReaderMock = new Mock<IDataReader>();
            dataReaderMock.SetupGet(x => x.FieldCount).Returns(3);
            dataReaderMock.SetupSequence(x => x.Read()).Returns(false);
            return dataReaderMock;
        }
    }

    public class DatabaseUrlTest : DatabaseUrlUtilities
    {
        protected static readonly object[] Actions =
        {
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ExecuteReader("QueryId"))
                , DefineExpr(x => x.ExecuteReader()) },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadScalar("QueryId"))
                , DefineExpr(x => x.ExecuteScalar(), "Value") },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadScalarNonNull("QueryId"))
                , DefineExpr(x => x.ExecuteScalar(), "Value") },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadSingle("QueryId"))
                , DefineExpr(x => x.ExecuteReader(), DefineDataReader().Object) },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadSingleNonNull("QueryId"))
                , DefineExpr(x => x.ExecuteReader(), DefineDataReader().Object) },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadFirst("QueryId"))
                , DefineExpr(x => x.ExecuteReader(), DefineDataReader().Object) },
            new object[] {
                (Action<DatabaseUrl>) ((DatabaseUrl db) => db.ReadFirstNonNull("QueryId"))
                , DefineExpr(x => x.ExecuteReader(), DefineDataReader().Object) }
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
            var reflectionCache = new Mock<IReflectionCache>();

            var db = CreateDbUrl(connectionUrlMock.Object, cpfMock.Object);
            dbAction.Invoke(db);

            connectionUrlMock.VerifyAll();
            connectionMock.VerifyAll();
            commandMock.VerifyAll();
            cpfMock.VerifyAll();
        }

        [Test]
        public void ReadFirst_TwoRowsReturned_OneRowReturned()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            var result = db.ReadFirst("QueryId");
            Assert.That(result, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(((dynamic)result).CustomerId, Is.EqualTo(5));
                Assert.That(((dynamic)result).FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(((dynamic)result).BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));
            });
        }

        [Test]
        public void ReadSingle_TwoRowsReturned_ThrowException()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            Assert.That(() => db.ReadSingle("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadMultiple_TwoRowsReturned_ReturnsTwoElements()
        {
            var db = GetDatabaseUrl(DefineTwoRows());
            var result = db.ReadMultiple("QueryId").ToList();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(((dynamic)result.ElementAt(0)).CustomerId, Is.EqualTo(5));
                Assert.That(((dynamic)result.ElementAt(0)).FullName, Is.EqualTo("Linus Torvalds"));
                Assert.That(((dynamic)result.ElementAt(0)).BirthDate, Is.EqualTo(new DateTime(1969, 12, 28)));

                Assert.That(((dynamic)result.ElementAt(1)).CustomerId, Is.EqualTo(4));
                Assert.That(((dynamic)result.ElementAt(1)).FullName, Is.EqualTo("Alan Turing"));
                Assert.That(((dynamic)result.ElementAt(1)).BirthDate, Is.EqualTo(new DateTime(1912, 6, 23)));
            });
            
        }


        [Test]
        public void ReadFirstNonNull_ZeroRowsReturned_ThrowsException()
        {
            var db = GetDatabaseUrl(DefineZeroRows());
            Assert.That(() => db.ReadFirstNonNull("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadSingleNonNull_ZeroRowsReturned_ThrowsException()
        {
            var db = GetDatabaseUrl(DefineZeroRows());
            Assert.That(() => db.ReadSingleNonNull("QueryId"), Throws.InvalidOperationException);
        }

        [Test]
        public void ReadFirst_ZeroRowsReturned_ReturnsNull()
        {
            var db = GetDatabaseUrl(DefineZeroRows());
            Assert.That(db.ReadFirst("QueryId"), Is.Null);
        }

        [Test]
        public void ReadSingle_ZeroRowsReturned_ReturnsNull()
        {
            var db = GetDatabaseUrl(DefineZeroRows());
            Assert.That(db.ReadSingle("QueryId"), Is.Null);
        }

        [Test]
        public void ReadMultiple_ZeroRowsReturned_ReturnsEmpty()
        {
            var db = GetDatabaseUrl(DefineZeroRows());
            Assert.That(db.ReadMultiple("QueryId"), Is.Empty);
        }
    }
}
