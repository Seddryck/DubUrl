using DubUrl.Provider.Odbc.Rewriting;
using NUnit.Framework;
using System.Diagnostics;
using System.Data;
using System.Data.Common;
using DubUrl.Registering;
using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;

namespace DubUrl.QA;

[FixtureLifeCycle(LifeCycle.SingleInstance)]
public abstract class BaseOleDbProvider
{
    protected ProviderCatalogBuilder Catalog { get; set; }

    [OneTimeSetUp]
    public void SetupFixture()
    {
        var assemblies = new[] { typeof(OdbcRewriter).Assembly, typeof(OleDbRewriter).Assembly };

        var discovery = new BinFolderDiscoverer(assemblies);
        var registrator = new ProviderFactoriesRegistrator(discovery);
        registrator.Register();

        Catalog = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule([.. new AssemblyTypesProbe().Assemblies, .. assemblies]));
    }

    public abstract string ConnectionString { get; }

    [Test]
    public void Connect()
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, Catalog.Build());
        Console.WriteLine(connectionUrl.Parse());

        using var conn = connectionUrl.Connect();
        Assert.That(conn.State, Is.EqualTo(ConnectionState.Closed));
    }

    [Test]
    public abstract void QueryCustomer();
    protected virtual void QueryCustomer(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, Catalog.Build());

        using var conn = connectionUrl.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        Assert.That(cmd.ExecuteScalar(), Is.EqualTo("Nikola Tesla"));
    }

    [Test]
    public abstract void QueryCustomerWithParams();
    protected virtual void QueryCustomerWithParams(string sql)
    {
        var connectionUrl = new ConnectionUrl(ConnectionString, Catalog.Build());

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
