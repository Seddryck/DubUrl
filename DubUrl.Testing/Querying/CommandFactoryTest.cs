using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
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
            var queryMock = new Mock<DubUrl.Querying.IQueryProvider>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            cmdMock.SetupSet(x => x.CommandType = CommandType.Text);
            cmdMock.SetupSet(x => x.CommandText = "select * from [table]");

            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var builder = new CommandBuilder();
            builder.Setup(queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            var cmd = builder.Execute(connMock.Object);
            Assert.That(cmd, Is.Not.Null);
            cmdMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_CommandFromConnection()
        {
            var queryMock = new Mock<DubUrl.Querying.IQueryProvider>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var builder = new CommandBuilder();
            builder.Setup(queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            var cmd = builder.Execute(connMock.Object);
            Assert.That(cmd, Is.Not.Null);
            connMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQuery_QueryRead()
        {
            var queryMock = new Mock<DubUrl.Querying.IQueryProvider>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var cmdMock = new Mock<IDbCommand>();
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var builder = new CommandBuilder();
            builder.Setup(queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            var cmd = builder.Execute(connMock.Object); ;
            Assert.That(cmd, Is.Not.Null);
            queryMock.VerifyAll();
        }

        [Test]
        public void Execute_ExistingQueryWithManyParameterss_CreateParemetersAndAddThemCalled()
        {
            var queryMock = new Mock<DubUrl.Querying.IQueryProvider>();
            queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            var paramMock = new Mock<IDbDataParameter>();
            var paramCollectionMock = new Mock<IDataParameterCollection>();
            paramCollectionMock.Setup(x => x.Add(It.IsAny<IDbDataParameter>()));
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);
            cmdMock.Setup(x => x.Parameters).Returns(paramCollectionMock.Object);
            var connMock = new Mock<IDbConnection>();
            connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            var builder = new CommandBuilder();
            builder.Setup(queryMock.Object, new MssqlDialect(new[] { "mssql" }));
            var parameters = new DubUrlParameter[]
            {
                new DubUrlParameterBoolean("IsValid", true)
                , new DubUrlParameterDateTime("From", new DateTime(2022, 09, 12, 21, 49, 16))
                , new DubUrlParameterDecimal("Value", 10, 4, 106522.1234m)
                , new DubUrlParameterStringUnicodeFixedLength("Name", 50, "Tesla")
                , new DubUrlParameterCurrency("Amount", null)
            };
            var cmd = builder.Execute(connMock.Object, parameters);
            Assert.That(cmd, Is.Not.Null);
            Assert.That(cmd.Parameters, Is.Not.Null);
            connMock.VerifyAll();
            cmdMock.Verify(x => x.CreateParameter(), Times.Exactly(5));
            paramCollectionMock.Verify(x => x.Add(It.IsAny<IDbDataParameter>()), Times.Exactly(5));
        }

        [Test]
        public void CreateParameter_DubUrlParameterBoolean_Exact()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.DbType = It.IsAny<DbType>());
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            paramMock.SetupSet(x => x.Value = It.IsAny<object?>());
            paramMock.SetupSet(x => x.Precision = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Scale = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Size = It.IsAny<int>());
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            var builder = new CommandBuilder();
            var param = builder.CreateParameter(cmdMock.Object, new DubUrlParameterBoolean("IsValid", true));

            paramMock.VerifySet(x => x.ParameterName = "IsValid", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.Boolean, Times.Once);
            paramMock.VerifySet(x => x.Value = true, Times.Once);
            paramMock.VerifySet(x => x.Precision = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Scale = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Size = It.IsAny<int>(), Times.Never);
        }

        [Test]
        public void CreateParameter_DubUrlParameterStringFixedLength_Exact()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.DbType = It.IsAny<DbType>());
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            paramMock.SetupSet(x => x.Value = It.IsAny<object?>());
            paramMock.SetupSet(x => x.Precision = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Scale = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Size = It.IsAny<int>());
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            var builder = new CommandBuilder();
            var param = builder.CreateParameter(cmdMock.Object, new DubUrlParameterStringUnicodeFixedLength("Name", 50, "My Text"));

            paramMock.VerifySet(x => x.ParameterName = "Name", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.StringFixedLength, Times.Once);
            paramMock.VerifySet(x => x.Value = "My Text", Times.Once);
            paramMock.VerifySet(x => x.Precision = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Scale = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Size = 50, Times.Once);
        }


        [Test]
        public void CreateParameter_DubUrlParameterString_Exact()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.DbType = It.IsAny<DbType>());
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            paramMock.SetupSet(x => x.Value = It.IsAny<object?>());
            paramMock.SetupSet(x => x.Precision = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Scale = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Size = It.IsAny<int>());
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            var builder = new CommandBuilder();
            var param = builder.CreateParameter(cmdMock.Object, new DubUrlParameterStringUnicode("Name", null));

            paramMock.VerifySet(x => x.ParameterName = "Name", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.String, Times.Once);
            paramMock.VerifySet(x => x.Value = DBNull.Value, Times.Once);
            paramMock.VerifySet(x => x.Precision = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Scale = It.IsAny<byte>(), Times.Never);
            paramMock.VerifySet(x => x.Size = It.IsAny<int>(), Times.Never);
        }

        [Test]
        public void CreateParameter_DubUrlParameterDecimal_Exact()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.DbType = It.IsAny<DbType>());
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            paramMock.SetupSet(x => x.Value = It.IsAny<object?>());
            paramMock.SetupSet(x => x.Precision = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Scale = It.IsAny<byte>());
            paramMock.SetupSet(x => x.Size = It.IsAny<int>());
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            var builder = new CommandBuilder();
            var param = builder.CreateParameter(cmdMock.Object, new DubUrlParameterDecimal("Value", 10, 4, 106522.1234m));

            paramMock.VerifySet(x => x.ParameterName = "Value", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.Decimal, Times.Once);
            paramMock.VerifySet(x => x.Value = 106522.1234m, Times.Once);
            paramMock.VerifySet(x => x.Precision = 10, Times.Once);
            paramMock.VerifySet(x => x.Scale = 4, Times.Once);
            paramMock.VerifySet(x => x.Size = It.IsAny<int>(), Times.Never);
        }
    }
}
