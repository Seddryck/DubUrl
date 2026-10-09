using System.Data;
using DubUrl.Registering;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[Category("ODBC")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class OdbcContract
{
    protected abstract string ConnectionUrl { get; }
    protected abstract string SelectFirstCustomerSql { get; }
    protected abstract string SelectCustomerByIdSql { get; }

    [OneTimeSetUp]
    public void RegisterProviderFactories()
        => new ProviderFactoriesRegistrator().Register();

    [Test]
    public void Connect()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test, Category("ConnectionUrl")]
    public void QueryCustomer()
        => AssertScalar(SelectFirstCustomerSql, null, 1, "Nikola Tesla");

    [Test, Category("ConnectionUrl")]
    public void QueryCustomerWithParams()
        => AssertScalar(SelectCustomerByIdSql, "CustId", 2, "Albert Einstein");

    private void AssertScalar(string sql, string? parameterName, int parameterValue, string expected)
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameterName is not null)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = parameterName;
            parameter.DbType = DbType.Int32;
            parameter.Value = parameterValue;
            command.Parameters.Add(parameter);
        }
        Assert.That(command.ExecuteScalar(), Is.EqualTo(expected));
    }
}
