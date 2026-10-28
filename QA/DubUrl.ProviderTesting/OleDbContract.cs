using System.Data;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class OleDbContract : CliConnectionCheckContract
{
    protected abstract ConnectionUrl CreateConnectionUrl();
    protected override string CliConnectionUrl => CreateConnectionUrl().Url;
    protected abstract string SelectFirstCustomerSql { get; }
    protected abstract string SelectCustomerByIdSql { get; }

    [Test, Category("OleDB")]
    public void Connect()
    {
        using var connection = CreateConnectionUrl().Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test, Category("OleDB")]
    public void QueryCustomer()
        => AssertScalar(SelectFirstCustomerSql, null, "Nikola Tesla");

    [Test, Category("OleDB")]
    public void QueryCustomerWithParams()
        => AssertScalar(SelectCustomerByIdSql, 2, "Albert Einstein");

    private void AssertScalar(string sql, int? parameterValue, string expected)
    {
        using var connection = CreateConnectionUrl().Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (parameterValue is not null)
        {
            var parameter = command.CreateParameter();
            parameter.DbType = DbType.Int32;
            parameter.Value = parameterValue.Value;
            command.Parameters.Add(parameter);
        }
        Assert.That(command.ExecuteScalar(), Is.EqualTo(expected));
    }
}
