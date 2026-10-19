using NUnit.Framework;
using System.Data;
using DubUrl.Registering;

namespace DubUrl.QA;


[Category("ODBC")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class BaseOdbcDriver
{
    [OneTimeSetUp]
    public void SetupFixture()
        => new ProviderFactoriesRegistrator().Register();

    public abstract string ConnectionString { get; }

    [Test]
    public void Connect()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString);
        Console.WriteLine(connectionUrl.Parse());

        using var conn = connectionUrl.Connect();
        Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    [Category("ConnectionUrl")]
    public abstract void QueryCustomer();
    protected void QueryCustomer(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    [Category("ConnectionUrl")]
    public abstract void QueryCustomerWithParams();
    protected void QueryCustomerWithParams(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = cmd.CreateParameter();
        param.ParameterName = "CustId";
        param.DbType = DbType.Int32;
        param.Value = 2;
        cmd.Parameters.Add(param);
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
    }
}