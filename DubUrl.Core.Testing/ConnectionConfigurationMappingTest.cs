using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class ConnectionConfigurationMappingTest
{
    private static ISchemeRegistry Registry(Type mapper, DbProviderFactory factory)
    {
        var mappings = AttributeProviderModule.Discover(mapper.Assembly).Where(info => info.MapperType == mapper);
        var builder = new ProviderCatalogBuilder();
        ProviderCatalogBuilder.RegisterMappings(builder, mappings, _ => factory);
        return builder.Build();
    }

    [TestCase("mssql", typeof(MsSqlServerMapper))]
    [TestCase("postgres", typeof(PostgresqlMapper))]
    [TestCase("mysql", typeof(MySqlConnectorMapper))]
    [TestCase("mariadb", typeof(MariaDbConnectorMapper))]
    public void NativeDrivers_ApplySameMappingsForStrictAndOptionalCalls(string scheme, Type mapper)
    {
        DbProviderFactory factory = scheme switch
        {
            "mssql" => SqlClientFactory.Instance,
            "postgres" => NpgsqlFactory.Instance,
            _ => MySqlConnectorFactory.Instance
        };
        var original = new ConnectionUrl($"{scheme}://alice@localhost/app", Registry(mapper, factory));
        var strict = original.WithApplicationName("Reporting;literal=1").WithPooling(new() { MinSize = 1, MaxSize = 12 })
            .WithConnectionTimeout(TimeSpan.FromSeconds(9)).WithCommandTimeout(TimeSpan.FromSeconds(11));
        var optional = original.WithOptional(options => options.WithApplicationName("Reporting;literal=1")
            .WithPooling(new() { MinSize = 1, MaxSize = 12 })
            .WithConnectionTimeout(TimeSpan.FromSeconds(9)).WithCommandTimeout(TimeSpan.FromSeconds(11)));
        Assert.That(optional.Parse(), Is.EqualTo(strict.Parse()));
        Assert.That(optional.SkippedOptions, Is.Empty);
        using var connection = (DbConnection)strict.Connect();
        Assert.That(connection.ConnectionTimeout, Is.EqualTo(9));
        using var command = connection.CreateCommand();
        Assert.That(command.CommandTimeout, Is.EqualTo(11));
        command.CommandTimeout = 25;
        Assert.That(command.CommandTimeout, Is.EqualTo(25));
        switch (connection)
        {
            case SqlConnection sql:
                var sqlBuilder = new SqlConnectionStringBuilder(sql.ConnectionString);
                Assert.That(sqlBuilder.ApplicationName, Is.EqualTo("Reporting;literal=1"));
                Assert.That(sqlBuilder.MaxPoolSize, Is.EqualTo(12));
                break;
            case NpgsqlConnection pg:
                var pgBuilder = new NpgsqlConnectionStringBuilder(pg.ConnectionString);
                Assert.That(pgBuilder.ApplicationName, Is.EqualTo("Reporting;literal=1"));
                Assert.That(pgBuilder.MaxPoolSize, Is.EqualTo(12));
                break;
            case MySqlConnection mysql:
                var mysqlBuilder = new MySqlConnectionStringBuilder(mysql.ConnectionString);
                Assert.That(mysqlBuilder.ApplicationName, Is.EqualTo("Reporting;literal=1"));
                Assert.That(mysqlBuilder.MaximumPoolSize, Is.EqualTo(12));
                break;
        }
    }

    [Test]
    public void Sqlite_SupportsPoolingOnlyAndDoesNotMapLockTimeoutAsCommandDeadline()
    {
        var original = new ConnectionUrl("sqlite://", Registry(typeof(SqliteMapper), SqliteFactory.Instance));
        var target = original.WithOptional(options => options.WithApplicationName("Reporting")
            .WithPooling(new() { Enabled = false })
            .WithConnectionTimeout(TimeSpan.FromSeconds(9)).WithCommandTimeout(TimeSpan.FromSeconds(11)));
        Assert.That(new SqliteConnectionStringBuilder(target.Parse()).Pooling, Is.False);
        Assert.That(target.SkippedOptions.Count, Is.EqualTo(3));
        Assert.Throws<NotSupportedException>(() => original.WithCommandTimeout(TimeSpan.FromSeconds(11)).Connect());
        Assert.Throws<NotSupportedException>(() => original.WithPooling(new() { MaxSize = 12 }).Connect());
    }

    [Test]
    public void ConfigurationAttributes_AreNotInheritedByCompatibleServers()
    {
        var assembly = typeof(PostgresqlMapper).Assembly;
        var mappings = AttributeProviderModule.Discover(assembly);
        Assert.That(mappings.Single(info => info.MapperType == typeof(PostgresqlMapper)).ConnectionConfiguration, Is.Not.Null);
        Assert.That(mappings.Where(info => info.MapperType != typeof(PostgresqlMapper))
            .Select(info => info.ConnectionConfiguration), Is.All.Null);
    }
}
