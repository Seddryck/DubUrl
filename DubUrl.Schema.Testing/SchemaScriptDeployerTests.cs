using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Moq;

namespace DubUrl.Schema.Testing;
public class SchemaScriptDeployerTests
{
    [Test]
    public void DeploySchema_AnyScript_UseCommand()
    {
        var cmd = new Mock<IDbCommand>();
        cmd.SetupSet(c => c.CommandText = It.IsAny<string>());
        cmd.Setup(c => c.ExecuteNonQuery());

        var connection = new Mock<IDbConnection>();
        connection.Setup(c => c.CreateCommand()).Returns(cmd.Object);

        var connectionUrl = new Mock<ConnectionUrl>("mssql://./db");
        connectionUrl.Setup(c => c.Open()).Returns(connection.Object);

        var deployer = new SchemaScriptDeployer();
        deployer.DeploySchema(connectionUrl.Object, "CREATE TABLE");

        connectionUrl.Verify(c => c.Open(), Times.Once);
        connection.Verify(c => c.CreateCommand(), Times.Once);
        cmd.VerifySet(c => c.CommandText = "CREATE TABLE", Times.Once);
        cmd.Verify(c => c.ExecuteNonQuery(), Times.Once);
    }
}
