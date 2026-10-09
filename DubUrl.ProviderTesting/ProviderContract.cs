using System.Data;
using DubUrl.Registering;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

[Category("Provider")]
[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class ProviderContract
{
    protected abstract string ConnectionUrl { get; }
    protected abstract string SelectFirstCustomerSql { get; }
    protected abstract string SelectCustomerByIdSql { get; }

    [OneTimeSetUp]
    public virtual void RegisterProviderFactories()
        => new ProviderFactoriesRegistrator().Register();

    [Test]
    [Category("Connection")]
    public void CreatesClosedConnection()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Connect();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    [Category("Connection")]
    public void OpensConnection()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    [Category("Querying")]
    public void ExecutesScalarQuery()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectFirstCustomerSql;
        Assert.That(command.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    [Category("Querying")]
    public void BindsNamedParameter()
    {
        using var connection = new ConnectionUrl(ConnectionUrl).Open();
        using var command = connection.CreateCommand();
        command.CommandText = SelectCustomerByIdSql;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "CustId";
        parameter.DbType = DbType.Int32;
        parameter.Value = 2;
        command.Parameters.Add(parameter);
        Assert.That(command.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
    }

    [Test]
    [Category("Querying")]
    public void ExecutesThroughDatabaseUrl()
    {
        var database = new DatabaseUrl(ConnectionUrl);
        Assert.That(database.ReadScalarNonNull<string>(SelectFirstCustomerSql), Is.EqualTo("Nikola Tesla"));
    }
}
