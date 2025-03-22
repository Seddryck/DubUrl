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

public class MsSqlServerBulkCopyEngineTests
{
    [Test]
    public void Write_CallDataReaderRead_Success()
    {
        var connectionUrl = new Mock<ConnectionUrl>("mssql://./db");
        var factory = new Mock<MsSqlServerBulkCopyFactory>();
        factory.Setup(x => x.Create(It.IsAny<DbConnection>(), It.IsAny<string>()))
            .Returns(new MsSqlServerBulkCopyProxy(
                writeToServer: reader =>
                {
                    reader.Read();
                    reader.Read();
                    reader.Read();
                },
                close: () => { }
            )).Verifiable();
        var dataReader = new Mock<IDataReader>();
        dataReader.Setup(x => x.Read()).Returns(new Queue<bool>(new[] { true, true, false }).Dequeue).Verifiable();

        var tableName = "Customer";
        var bulkCopyEngine = new MsSqlServerBulkCopyEngine(connectionUrl.Object, factory.Object);
        bulkCopyEngine.Write(tableName, dataReader.Object);
        factory.VerifyAll();
        dataReader.Verify(x => x.Read(), Times.Exactly(3));
    }
}
