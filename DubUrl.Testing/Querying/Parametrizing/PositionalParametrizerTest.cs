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

namespace DubUrl.Testing.Querying.Parametrizing
{
    public class PositionalParametrizerTest
    {

        [Test]
        public void CreateParameter_DubUrlParameterBoolean_NoNameAssigned()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            var parametrizer = new PositionalParametrizer();
            var param = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterBoolean("IsValid", true));

            paramMock.VerifySet(x => x.ParameterName = It.IsAny<string>(), Times.Never);
        }
    }
}
