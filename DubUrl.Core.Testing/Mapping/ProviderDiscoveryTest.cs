using DubUrl.Provider.Db2.Mapping;
using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Provider.FirebirdSql.Mapping;
using DubUrl.Provider.MySql.Mapping;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.Oracle.Mapping;
using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.Snowflake.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Provider.Teradata.Mapping;
using DubUrl.Provider.Trino.Mapping;
using DubUrl.Mapping;
using NUnit.Framework;
using System;
using System.Data.Common;
using System.Runtime.InteropServices;
using SingleStoreConnector;

namespace DubUrl.Core.Testing.Mapping;

public class ProviderDiscoveryTest
{
    [SetUp]
    public void DefaultRegistration()
    {
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        DbProviderFactories.RegisterFactory("IBM.Data.Db2", IBM.Data.Db2.DB2Factory.Instance);
        DbProviderFactories.RegisterFactory("Snowflake.Data", Snowflake.Data.Client.SnowflakeDbFactory.Instance);
        DbProviderFactories.RegisterFactory("Teradata.Client", Teradata.Client.Provider.TdFactory.Instance);
        DbProviderFactories.RegisterFactory("FirebirdSql.Data.FirebirdClient", FirebirdSql.Data.FirebirdClient.FirebirdClientFactory.Instance);
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
        DbProviderFactories.RegisterFactory("NReco.PrestoAdo", NReco.PrestoAdo.PrestoDbFactory.Instance);
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDB.NET.Data.DuckDBClientFactory.Instance);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            DbProviderFactories.RegisterFactory("System.Data.OleDb", System.Data.OleDb.OleDbFactory.Instance);
    }

    [Test]
    [TestCase("oracle", typeof(OracleManagedDataAccessMapper))]
    [TestCase("mysql", typeof(MySqlConnectorMapper))]
    [TestCase("mssql", typeof(MsSqlServerMapper))]
    [TestCase("pgsql", typeof(PostgresqlMapper))]
    [TestCase("db2", typeof(Db2Mapper))]
    [TestCase("sqlite", typeof(SqliteMapper))]
    [TestCase("maria", typeof(MariaDbConnectorMapper))]
    [TestCase("sf", typeof(SnowflakeMapper))]
    [TestCase("td", typeof(TeradataMapper))]
    [TestCase("fb", typeof(FirebirdSqlMapper))]
    [TestCase("cr", typeof(CockRoachMapper))]
    [TestCase("ts", typeof(TimescaleMapper))]
    [TestCase("quest", typeof(QuestDbMapper))]
    [TestCase("tr", typeof(TrinoMapper))]
    [TestCase("single", typeof(SingleStoreMapper))]
    [TestCase("crate", typeof(CrateDbMapper))]
    [TestCase("duck", typeof(DuckdbMapper))]
    [TestCase("odbc+mssql", typeof(OdbcMapper))]
    [TestCase("mssql+odbc", typeof(OdbcMapper))]
    public void Instantiate_Scheme_CorrectType(string scheme, Type expected)
    {
        var builder = new ProviderCatalogBuilder().AddApplicationProviders();
        var registry = builder.Build();
        var result = registry.GetMapper(scheme.Split(['+', ':']));

        Assert.Multiple(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.TypeOf(expected));
                Assert.That(result, Is.Not.Null);
            });
            Assert.That(registry.CanHandle(scheme), Is.True);
        });
    }
}
