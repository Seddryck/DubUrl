using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DuckDB.NET.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Moq;
using Npgsql;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Testing;

public class ConnectionReusePolicyTest
{
    private sealed class Policy(ConnectionReuseMode? decision) : IConnectionReusePolicyResolver
    {
        public int Calls { get; private set; }
        public ConnectionReuseMode? Resolve(ConnectionReuseContext context) { Calls++; return decision; }
    }

    internal static ConnectionSource CreateSource(DbProviderFactory factory, string connectionString, ConnectionSourceOptions? options = null)
    {
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(new ResolvedConnection(
            connectionString, Mock.Of<IDialect>(), Mock.Of<IConnectivity>(), Mock.Of<IParametrizer>(), factory));
        return new ConnectionSourceUrl("test://localhost", registry.Object).Create(options);
    }

    [TestCase("npgsql", "Host=localhost", "provider-pooling")]
    [TestCase("npgsql", "Server=localhost;Pooling=false", "pooling-disabled-no-retention-policy")]
    [TestCase("sqlclient", "Server=localhost;Pooling=yes", "provider-pooling")]
    [TestCase("sqlclient", "Server=localhost;Pooling=no", "pooling-disabled-no-retention-policy")]
    [TestCase("sqlite", "DataSource=:memory:", "provider-pooling")]
    [TestCase("sqlite", "Data Source=:memory:;Pooling=false", "pooling-disabled-no-retention-policy")]
    public void Auto_UsesProviderBuildersAndEffectivePoolingConfiguration(string provider, string configuration, string reason)
    {
        DbProviderFactory factory = provider switch
        {
            "npgsql" => NpgsqlFactory.Instance,
            "sqlclient" => SqlClientFactory.Instance,
            _ => SqliteFactory.Instance
        };
        using var source = CreateSource(factory, configuration, new() { ReuseMode = ConnectionReuseMode.Auto });
        Assert.That(source.ReuseMode, Is.EqualTo(ConnectionReuseMode.ProviderManaged));
        Assert.That(source.SelectionReason, Is.EqualTo(reason));
    }

    [Test]
    public void ExplicitMode_SkipsRegisteredPolicies()
    {
        var policy = new Policy(ConnectionReuseMode.RetainedExclusive);
        using var source = ConnectionSourceTest.CreateUrl(() => new ConnectionSourceTest.TestConnection()).Create(
            new() { PolicyResolvers = [policy] });
        Assert.That(policy.Calls, Is.Zero);
        Assert.That(source.ReuseMode, Is.EqualTo(ConnectionReuseMode.ProviderManaged));
    }

    [Test]
    public async Task RegisteredRetentionPolicy_OverridesBuiltInAndIsEvaluatedOnce()
    {
        var policy = new Policy(ConnectionReuseMode.RetainedExclusive);
        using var source = ConnectionSourceTest.CreateUrl(() => new ConnectionSourceTest.TestConnection()).Create(
            new() { ReuseMode = ConnectionReuseMode.Auto, PolicyResolvers = [new Policy(null), policy] });
        DbConnection connection;
        using (var first = source.Acquire()) connection = first.Connection;
        await using (var second = await source.AcquireAsync())
            Assert.That(second.Connection, Is.SameAs(connection));
        Assert.That(policy.Calls, Is.EqualTo(1));
        Assert.That(source.SelectionReason, Is.EqualTo("registered-policy"));
    }

    [Test]
    public void RegisteredPolicy_OverridesKnownProviderWithPoolingDisabled()
    {
        using var source = CreateSource(NpgsqlFactory.Instance, "Host=localhost;Pooling=false", new()
        {
            ReuseMode = ConnectionReuseMode.Auto,
            PolicyResolvers = [new Policy(ConnectionReuseMode.RetainedExclusive)]
        });
        Assert.That(source.ReuseMode, Is.EqualTo(ConnectionReuseMode.RetainedExclusive));
    }

    [Test]
    public async Task UnknownProvider_FallsBackToIndependentLeasesWithoutProbing()
    {
        var creates = 0;
        await using var source = ConnectionSourceTest.CreateUrl(() => { creates++; return new ConnectionSourceTest.TestConnection(); }).Create(
            new() { ReuseMode = ConnectionReuseMode.Auto });
        Assert.That(creates, Is.Zero);
        DbConnection connection;
        using (var first = source.Acquire()) connection = first.Connection;
        await using (var second = await source.AcquireAsync())
            Assert.That(second.Connection, Is.Not.SameAs(connection));
        Assert.That(source.SelectionReason, Is.EqualTo("unknown-configuration"));
    }

    [Test]
    public void SameSchemeWithDifferentProviderRoutes_DoesNotInheritNativePolicy()
    {
        var context = new ConnectionReuseContext(System.Data.Odbc.OdbcFactory.Instance, "Driver=PostgreSQL");
        var selection = ConnectionReusePolicy.Select(new() { ReuseMode = ConnectionReuseMode.Auto }, context);
        Assert.That(selection.Reason, Is.EqualTo("unknown-configuration"));
        if (OperatingSystem.IsWindows())
        {
            var oledb = new ConnectionReuseContext(System.Data.OleDb.OleDbFactory.Instance, "Provider=MSOLEDBSQL");
            Assert.That(ConnectionReusePolicy.Select(new() { ReuseMode = ConnectionReuseMode.Auto }, oledb).Reason,
                Is.EqualTo("unknown-configuration"));
        }
    }

    [Test]
    public void InvalidConfiguration_RaisesRedactedValidationError()
    {
        var context = new ConnectionReuseContext(NpgsqlFactory.Instance, "Pooling=not-a-boolean-secret");
        var exception = Assert.Throws<ArgumentException>(() => ConnectionReusePolicy.Select(new() { ReuseMode = ConnectionReuseMode.Auto }, context));
        Assert.That(exception!.ToString(), Does.Not.Contain("not-a-boolean-secret"));
        Assert.That(context.ToString(), Does.Not.Contain("not-a-boolean-secret"));
    }

    [Test]
    public void ResolverMustReturnConcreteMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ConnectionSourceTest.CreateUrl(() => new ConnectionSourceTest.TestConnection())
            .Create(new() { ReuseMode = ConnectionReuseMode.Auto, PolicyResolvers = [new Policy(ConnectionReuseMode.Auto)] }));
    }

    [Test]
    public async Task DuckDbLocal_AutomaticallyRetainsDatabaseAcrossSequentialLeases()
    {
        await using var source = CreateSource(DuckDBClientFactory.Instance, "DataSource=:memory:", new() { ReuseMode = ConnectionReuseMode.Auto });
        using (var lease = source.Acquire())
        using (var command = lease.Connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE reuse_test AS SELECT 42 AS value";
            command.ExecuteNonQuery();
        }
        await using (var lease = await source.AcquireAsync())
        await using (var command = lease.Connection.CreateCommand())
        {
            command.CommandText = "SELECT value FROM reuse_test";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(42));
        }
        Assert.That(source.SelectionReason, Is.EqualTo("duckdb-local-retained"));
    }
}
