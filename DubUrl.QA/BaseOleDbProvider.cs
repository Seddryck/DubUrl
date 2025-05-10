using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using DubUrl.Mapping.Implementation;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.QA;

[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class BaseOleDbProvider
{
    protected SchemeRegistryBuilder SchemeRegistryBuilder { get; set; }

    [OneTimeSetUp]
    public void SetupFixture()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };

        var discovery = new BinFolderDiscoverer(assemblies);
        var registrator = new ProviderFactoriesRegistrator(discovery);
        registrator.Register();

        SchemeRegistryBuilder = new SchemeRegistryBuilder()
            .WithAssemblies(assemblies)
            .WithAutoDiscoveredMappings();
    }

    public abstract string ConnectionString { get; }

    [Test]
    public void Connect()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeRegistryBuilder);
        Console.WriteLine(connectionUrl.Parse());

        using var conn = connectionUrl.Connect();
        Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public abstract void QueryCustomer();
    protected virtual void QueryCustomer(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeRegistryBuilder);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    public abstract void QueryCustomerWithParams();
    protected virtual void QueryCustomerWithParams(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, SchemeRegistryBuilder);

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var param = cmd.CreateParameter();
        param.DbType = DbType.Int32;
        param.Value = 2;
        cmd.Parameters.Add(param);
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Albert Einstein"));
    }
}
