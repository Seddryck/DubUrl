using DubUrl.Querying;
using DubUrl.Querying.Dialects;
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

namespace DubUrl.Testing.Parametrizing
{
    public class NamedParametrizerTest
    {

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

            var parametrizer = new NamedParametrizer();
            var param = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterBoolean("IsValid", true));

            paramMock.VerifySet(x => x.ParameterName = "IsValid", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.Boolean, Times.Once);
            paramMock.VerifySet(x => x.Value = true, Times.Once);
            paramMock.VerifySet(x => x.Precision = default, Times.Once);
            paramMock.VerifySet(x => x.Scale = default, Times.Once);
            paramMock.VerifySet(x => x.Size = default, Times.Once);
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

            var parametrizer = new NamedParametrizer();
            var param = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterStringUnicodeFixedLength("Name", 50, "My Text"));

            paramMock.VerifySet(x => x.ParameterName = "Name", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.StringFixedLength, Times.Once);
            paramMock.VerifySet(x => x.Value = "My Text", Times.Once);
            paramMock.VerifySet(x => x.Precision = default, Times.Once);
            paramMock.VerifySet(x => x.Scale = default, Times.Once);
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

            var parametrizer = new NamedParametrizer();
            var param = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterStringUnicode("Name", null));

            paramMock.VerifySet(x => x.ParameterName = "Name", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.String, Times.Once);
            paramMock.VerifySet(x => x.Value = DBNull.Value, Times.Once);
            paramMock.VerifySet(x => x.Precision = default, Times.Once);
            paramMock.VerifySet(x => x.Scale = default, Times.Once);
            paramMock.VerifySet(x => x.Size = default, Times.Once);
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

            var parametrizer = new NamedParametrizer();
            var param = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterDecimal("Value", 10, 4, 106522.1234m));

            paramMock.VerifySet(x => x.ParameterName = "Value", Times.Once);
            paramMock.VerifySet(x => x.DbType = DbType.Decimal, Times.Once);
            paramMock.VerifySet(x => x.Value = 106522.1234m, Times.Once);
            paramMock.VerifySet(x => x.Precision = 10, Times.Once);
            paramMock.VerifySet(x => x.Scale = 4, Times.Once);
            paramMock.VerifySet(x => x.Size = default, Times.Once);
        }
    }
}
