using NUnit.Framework;
using DuckDB.NET.Data;
using DuckDB.NET;
using System.Data;
using DubUrl.Schema;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Renderers;
using Microsoft.Extensions.Options;

namespace DubUrl.QA.DuckDB;

[Category("DuckDB")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
[NonParallelizable]
public class AdoProviderDuckDB : BaseAdoProvider
{
    public override string ConnectionString
        => $"duckdb:///customer.duckdb";

    [Test]
    public override void QueryCustomer()
        => QueryCustomer("select FullName from Customer where CustomerId=1");

    [Test]
    public override void QueryCustomerWithDatabase()
        => QueryCustomerWithDatabase("select FullName from Customer where CustomerId=1");

    [Test]
    public override void QueryCustomerWithParams()
        => Assert.Ignore("Named parameters not supported by DuckDB");

    [Test]
    public override void QueryCustomerWithPositionalParameter()
        => QueryCustomerWithPositionalParameter("select FullName from Customer where CustomerId=($1)");

    [Test]
    public override void QueryCustomerWithDapper()
        => QueryCustomerWithDapper("select * from Customer");

    [Test]
    public override void QueryCustomerWithDbReader()
        => QueryCustomerWithDbReader("select * from Customer");

    [Test]
    public override void QueryCustomerWithWhereClause()
        => Assert.Ignore("Object of type 'DuckDB.NET.DuckDBDateOnly' cannot be converted to type 'System.DateTime'");

    [Test]
    public override void QueryIntervalWithDatabaseUrl()
        => Assert.Ignore("Awaiting resolution of https://github.com/Giorgi/DuckDB.NET/issues/111");

    [Test]
    public void CreateTable()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString);
        connectionUrl.DeploySchema(s =>
            s.WithTables(tables =>
                tables.Add(t =>
                    t.WithName("Sales")
                    .WithColumns(cols =>
                        cols.Add(col => col.WithName("SalesId").WithType(DbType.Int64))
                            .Add(col => col.WithName("CustomerId").WithType(DbType.Int32))
                            .Add(col => col.WithName("Amount").WithType(DbType.Decimal).WithPrecision(10).WithScale(2))
                    )
                    .WithConstraints(constraints =>
                        constraints.AddPrimaryKey(pk => pk.WithColumnName("CustomerId")
                    )
                )
            )
        ), SchemaCreationOptions.DropIfExists);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "select count(*) from Sales;";
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo(0));
    }

}
