using DubUrl.Mapping;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Querying.Reading;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading
{
    public class ParametrizedEmbeddedSqlFileCommandTest
    {

        [Test]
        public void Execute_ExistingQueryWithManyParameterss_CreateParemetersAndAddThemCalled()
        {
            //var resourceManager = new Mock<IResourceManager>();
            //resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<string[]>())).Returns(true);
            //resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<string[]>())).Returns("");

            //var dialectMock = new Mock<IDialect>();
            //dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            //var queryMock = new Mock<ICommandProvider>();
            //queryMock.Setup(x => x.Exists(It.IsAny<IDialect>(), It.IsAny<bool>())).Returns(true);
            //queryMock.Setup(x => x.Read(It.IsAny<IDialect>())).Returns("select * from [table]");

            //var paramMock = new Mock<IDbDataParameter>();
            //var paramCollectionMock = new Mock<IDataParameterCollection>();
            //paramCollectionMock.Setup(x => x.Add(It.IsAny<IDbDataParameter>()));

            //var cmdMock = new Mock<IDbCommand>();
            //cmdMock.SetupGet(x => x.Parameters).Returns(paramCollectionMock.Object);
            //cmdMock.Setup(x => x.CreateParameter()).Returns(paramMock.Object);

            //var connMock = new Mock<IDbConnection>();
            ////connMock.Setup(x => x.CreateCommand()).Returns(cmdMock.Object);

            //var parameters = new DubUrlParameter[]
            //{
            //    new DubUrlParameterBoolean("IsValid", true)
            //    , new DubUrlParameterDateTime("From", new DateTime(2022, 09, 12, 21, 49, 16))
            //    , new DubUrlParameterDecimal("Value", 10, 4, 106522.1234m)
            //    , new DubUrlParameterStringUnicodeFixedLength("Name", 50, "Tesla")
            //    , new DubUrlParameterCurrency("Amount", null)
            //};

            //var commandProvider = new ParametrizedEmbeddedSqlFileCommand(resourceManager.Object, "myQuery", parameters);
            //var connectionUrlMock = new Mock<ConnectionUrl>(new[] { "mssql://localhost/db" });
            //connectionUrlMock.Setup(x => x.CreateCommand(It.IsAny<ICommandProvider>())).Returns(cmdMock.Object);
            //var cmd = connectionUrlMock.Object.CreateCommand(commandProvider);
            //Assert.That(cmd, Is.Not.Null);
            //Assert.That(cmd.Parameters, Is.Not.Null);
            //connMock.VerifyAll();
            //cmdMock.Verify(x => x.CreateParameter(), Times.Exactly(5));
            //paramCollectionMock.Verify(x => x.Add(It.IsAny<IDbDataParameter>()), Times.Exactly(5));
        }

        [Test]
        public void Read_AnyExistingResources_InvokeLog()
        {
            var resourceManager = new Mock<IResourceManager>();
            resourceManager.Setup(x => x.Any(It.IsAny<string>(), It.IsAny<DirectCommandMatchingOption>())).Returns(true);
            resourceManager.Setup(x => x.BestMatch(It.IsAny<string>(), It.IsAny<DirectCommandMatchingOption>())).Returns("foo");
            resourceManager.Setup(x => x.ReadResource(It.IsAny<string>())).Returns("bar");

            var dialectMock = new Mock<IDialect>();
            dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            var connectivityMock = new Mock<IConnectivity>();
            connectivityMock.SetupGet(x => x.Alias).Returns(string.Empty);

            var queryLoggerMock = new Mock<IQueryLogger>();

            var query = new ParametrizedEmbeddedSqlFileCommand(resourceManager.Object, "foo", Array.Empty<DubUrlParameter>(), queryLoggerMock.Object);
            var result = query.Read(dialectMock.Object, connectivityMock.Object);

            queryLoggerMock.Verify(log => log.Log(It.IsAny<string>()));
        }
    }
}
