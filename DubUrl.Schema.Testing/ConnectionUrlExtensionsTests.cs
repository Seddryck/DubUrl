using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Builders;
using Moq;
using NUnit.Framework;

namespace DubUrl.Schema.Testing;
public class ConnectionUrlExtensionsTests
{
    [Test]
    public void DeploySchema_SingleTable_ExpectedCalls()
    {
        var dbCommand = new Mock<IDbCommand>();

        var dbConnection = new Mock<IDbConnection>();
        dbConnection.Setup(c => c.CreateCommand()).Returns(dbCommand.Object);

        var dbTypeMapper = new Mock<IDbTypeMapper>();
        dbTypeMapper.Setup(x => x.ToDictionary()).Returns(new Dictionary<string, object>());

        var renderer = new Mock<IRenderer>();
        renderer.Setup(x => x.Render(It.IsAny<object?>(), It.IsAny<string>()));

        var dialect = new Mock<IDialect>();
        dialect.Setup(x => x.DbTypeMapper).Returns(dbTypeMapper.Object);
        dialect.Setup(x => x.Renderer).Returns(renderer.Object);

        var connectionUrl = new Mock<ConnectionUrl>(It.IsAny<string>());
        connectionUrl.Setup(c => c.Open()).Returns(dbConnection.Object);
        connectionUrl.SetupGet(c => c.Dialect).Returns(dialect.Object);

        var schema = new SchemaBuilder()
            .WithTables(tables =>
                tables.Add(table =>
                    table.WithName("Customer")
                        .WithColumns(cols =>
                            cols.Add(col => col.WithName("Id").WithType(DbType.Int32))
                                .Add(col => col.WithName("FullName").WithType(DbType.String).WithLength(120))
                        )
                )).Build();

        ConnectionUrlExtensions.DeploySchema(connectionUrl.Object, schema);
        connectionUrl.Verify(x => x.Open(), Times.Once);
        dbConnection.Verify(x => x.CreateCommand(), Times.Once);
        dbCommand.VerifySet(x => x.CommandText = It.IsAny<string>(), Times.Once);
        dbCommand.Verify(x => x.ExecuteNonQuery(), Times.Once);
    }
}
