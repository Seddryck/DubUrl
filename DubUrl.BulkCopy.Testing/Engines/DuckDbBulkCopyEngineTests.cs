using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.BulkCopy.Engines;
using Moq;
using NUnit.Framework;

namespace DubUrl.BulkCopy.Testing.Engines;

public class DuckDbBulkCopyEngineTests
{
    [Test]
    public void Write_CallDataReaderRead_Success()
    {
        var connectionUrl = new Mock<ConnectionUrl>("duck://memory");

        var row = new Mock<DuckDbAppenderRowProxy>(() => { }, (object value) => { }, () => { });

        var factory = new Mock<DuckDbAppenderFactory>();
        factory.Setup(x => x.CreateAppender(It.IsAny<IDbConnection>(), It.IsAny<string>()))
            .Returns(new DuckDbAppenderProxy(
                createRow: () => row.Object,
                close: () => { }
            )).Verifiable();
        var dataReader = new Mock<IDataReader>();
        dataReader.Setup(x => x.Read()).Returns(new Queue<bool>(new[] { true, true, false }).Dequeue).Verifiable();

        var tableName = "Customer";
        var bulkCopyEngine = new DuckDbBulkCopyEngine(connectionUrl.Object, factory.Object);
        bulkCopyEngine.Write(tableName, dataReader.Object);
        factory.VerifyAll();
        dataReader.Verify(x => x.Read(), Times.Exactly(3));
    }

    [Test]
    public void Write_CallDataReaderIndex_Success()
    {
        var connectionUrl = new Mock<ConnectionUrl>("duck://memory");

        var row = new Mock<DuckDbAppenderRowProxy>(() => { }, (object value) => { }, () => { });

        var factory = new Mock<DuckDbAppenderFactory>();
        factory.Setup(x => x.CreateAppender(It.IsAny<IDbConnection>(), It.IsAny<string>()))
            .Returns(new DuckDbAppenderProxy(
                createRow: () => row.Object,
                close: () => { }
            )).Verifiable();

        int i =0;
        var dataReader = new Mock<IDataReader>();
        dataReader.Setup(x => x.Read()).Returns(new Queue<bool>(new[] { true, true, false }).Dequeue).Verifiable();
        dataReader.SetupGet(x => x.FieldCount).Returns(2).Verifiable();
        dataReader.Setup(x => x[0]).Returns(() => ++i).Verifiable();
        dataReader.Setup(x => x[1]).Returns(new Queue<string>(["foo", "bar"]).Dequeue).Verifiable();

        var tableName = "Customer";
        var bulkCopyEngine = new DuckDbBulkCopyEngine(connectionUrl.Object, factory.Object);
        bulkCopyEngine.Write(tableName, dataReader.Object);
        factory.VerifyAll();
        dataReader.Verify(x => x.FieldCount, Times.AtLeast(2));
        dataReader.Verify(x => x[0], Times.Exactly(2));
        dataReader.Verify(x => x[1], Times.Exactly(2));
    }
}
