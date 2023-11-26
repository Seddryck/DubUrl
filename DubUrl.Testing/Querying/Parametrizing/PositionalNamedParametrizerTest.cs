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
    public class PositionalNamedParametrizerTest
    {

        [Test]
        public void CreateParameter_DubUrlParameterBoolean_NameAssignedWithPosition()
        {
            var paramMock = new Mock<IDbDataParameter>();
            paramMock.SetupSet(x => x.ParameterName = It.IsAny<string>());

            var dataParameterCollectionMock = new Mock<IDataParameterCollection>();
            dataParameterCollectionMock.SetupGet(x => x.Count).Returns(0);

            var cmdMock = new Mock<IDbCommand>();
            cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);
            cmdMock.SetupGet(x => x.Parameters).Returns(dataParameterCollectionMock.Object);

            var parametrizer = new PositionalNamedParametrizer();
            var param1 = parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterBoolean("IsValid", true));
            paramMock.VerifySet(x => x.ParameterName = "1", Times.Once);
        }

        [Test]
        public void CreateParameter_TwoParameters_NameAssignedWithPosition()
        {
            var param1Mock = new Mock<IDbDataParameter>();
            param1Mock.SetupSet(x => x.ParameterName = It.IsAny<string>());
            var param2Mock = new Mock<IDbDataParameter>();
            param2Mock.SetupSet(x => x.ParameterName = It.IsAny<string>());

            var count = 0;
            var dataParameterCollectionMock = new Mock<IDataParameterCollection>();
            dataParameterCollectionMock.Setup(x => x.Add(It.IsAny<IDbDataParameter>())).Callback(() => count+=1);
            dataParameterCollectionMock.SetupGet(x => x.Count).Returns(() => count);

            var cmdMock = new Mock<IDbCommand>();
            cmdMock.SetupSequence(x => x.CreateParameter())
                .Returns(param1Mock.Object)
                .Returns(param2Mock.Object);
            cmdMock.SetupGet(x => x.Parameters).Returns(dataParameterCollectionMock.Object);



            var parametrizer = new PositionalNamedParametrizer();
            cmdMock.Object.Parameters.Add(
                parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterBoolean("IsValid", true))
            );
            cmdMock.Object.Parameters.Add(
                parametrizer.CreateParameter(cmdMock.Object, new DubUrlParameterInt("Value", 32))
            );
            
            param1Mock.VerifySet(x => x.ParameterName = "1", Times.Once);
            param2Mock.VerifySet(x => x.ParameterName = "2", Times.Once);
            Assert.That(cmdMock.Object.Parameters, Has.Count.EqualTo(2));
        }
    }
}
