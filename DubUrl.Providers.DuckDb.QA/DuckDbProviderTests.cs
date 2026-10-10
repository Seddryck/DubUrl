using System.Data;
using DubUrl.BulkCopy;
using DubUrl.ProviderTesting;
using DubUrl.Schema;
using Moq;
using NUnit.Framework;

namespace DubUrl.Providers.DuckDb.QA;

[TestFixture, Category("DuckDB"), NonParallelizable]
public sealed class DuckDbProviderTests : ProviderContract
{
    protected override string ConnectionUrl => DuckDbTestDatabase.ConnectionUrl;
    protected override string SelectFirstCustomerSql => "select FullName from Customer where CustomerId=1";
    protected override string SelectCustomerByIdSql => string.Empty;
    protected override string SelectCustomerByPositionSql => "select FullName from Customer where CustomerId=($1)";
    protected override string SelectAllCustomersSql => "select * from Customer";
    protected override string SelectYoungestCustomersSql => "select CustomerId, FullName, cast(BirthDate as DateTime) as \"BirthDate\" from Customer order by BirthDate desc limit $count$";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | $field; format="identity"$}; separator=", "$ from $table; format="identity"$
        where $clauses:{clause | $clause.Field; format="identity"$ $clause.Operator$ $clause.Value; format="value"$}; separator=" and "$
        """;
    protected override bool SupportsNamedParameters => false;
    protected override bool SupportsInterval => false;

    [Test]
    public void CreateTable()
    {
        var connection = new ConnectionUrl(ConnectionUrl);
        connection.DeploySchema(s => s.WithTables(t => t.Add(x => x.WithName("Sales").WithColumns(c => c
            .Add(y => y.WithName("SalesId").WithType(DbType.Int64))
            .Add(y => y.WithName("CustomerId").WithType(DbType.Int32))
            .Add(y => y.WithName("Amount").WithType(DbType.Decimal).WithPrecision(10).WithScale(2))
        ))), SchemaCreationOptions.DropIfExists);
        AssertTableEmpty(connection, "Sales");
    }

    [Test]
    public void CreateIndex()
    {
        var connection = new ConnectionUrl(ConnectionUrl);
        connection.DeploySchema(
            schema => schema
                .WithTables(tables => tables.Add(table => table
                    .WithName("Orders")
                    .WithColumns(columns => columns
                        .Add(column => column.WithName("OrderId").WithType(DbType.Int64))
                        .Add(column => column.WithName("ProductId").WithType(DbType.Int16))
                        .Add(column => column.WithName("CustomerId").WithType(DbType.Int32)))))
                .WithIndexes(indexes => indexes.Add(index => index
                    .WithName("idx_ProductId")
                    .OnTable("Orders")
                    .WithColumns(columns => columns.Add(column => column.WithName("ProductId"))))),
            SchemaCreationOptions.DropIfExists);
        AssertTableEmpty(connection, "Orders");
    }

    [Test]
    public void BulkCopy()
    {
        var connection = new ConnectionUrl(ConnectionUrl);
        using (var setupConnection = connection.Open()) { using var setupCommand = setupConnection.CreateCommand(); setupCommand.CommandText = "DROP TABLE IF EXISTS Sales; CREATE TABLE Sales (id INTEGER, amount DECIMAL(10,2));"; setupCommand.ExecuteNonQuery(); }
        var index = 0;
        var reader = new Mock<IDataReader>();
        reader.Setup(x => x.Read()).Returns(new Queue<bool>([true, true, true, false]).Dequeue);
        reader.SetupGet(x => x.FieldCount).Returns(2);
        reader.Setup(x => x[0]).Returns(() => ++index);
        reader.Setup(x => x[1]).Returns(new Queue<decimal>([10.2m, 105.23m, 1500m]).Dequeue);
        connection.BulkCopy("Sales", reader.Object);
        using var db = connection.Open(); using var command = db.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Sales;";
        Assert.That(command.ExecuteScalar(), Is.EqualTo(3));
    }

    private static void AssertTableEmpty(ConnectionUrl connection, string table)
    {
        using var db = connection.Open(); using var command = db.CreateCommand(); command.CommandText = $"select count(*) from {table};";
        Assert.That(command.ExecuteScalar(), Is.EqualTo(0));
    }
}
