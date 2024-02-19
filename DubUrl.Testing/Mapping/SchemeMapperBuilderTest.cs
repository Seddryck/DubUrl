using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping;
using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Rewriting.Tokening;
using DubUrl.Rewriting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Rewriting.Implementation;
using SingleStoreConnector;

namespace DubUrl.Testing.Mapping;

public class SchemeMapperBuilderTest
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

    private class StubMapper : BaseMapper
    {
        public StubMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new StubRewriter(csb), dialect, parametrizer) { }

        private class StubRewriter : ConnectionStringRewriter
        {
            public StubRewriter(DbConnectionStringBuilder csb)
                : base(new Specificator(csb), []) { }
        }
    }

    private class FakeMapper : BaseMapper
    {
        public FakeMapper(DbConnectionStringBuilder csb, IDialect dialect, IParametrizer parametrizer)
            : base(new FakeRewriter(csb), dialect, parametrizer) { }

        private class FakeRewriter : ConnectionStringRewriter
        {
            public FakeRewriter(DbConnectionStringBuilder csb)
                : base(new Specificator(csb), []) { }
        }
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
        var schemeMapper = new SchemeMapperBuilder().Build();
        Assert.That(schemeMapper.CanHandle(scheme), Is.True);
        var result = schemeMapper.GetMapper(scheme);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf(expected));
            Assert.That(result, Is.Not.Null);
        });
    }

    [Test]
    [Ignore("To be re-implemented")]
    public void AddAlias_NewScheme_CorrectType()
    {
        var weirdScheme = "xyz";

        var builder = new SchemeMapperBuilder();
        var schemeMapper = builder.Build();
        Assert.Catch<SchemeNotFoundException>(() => schemeMapper.GetMapper(weirdScheme)); //Should not exists

        builder.AddAlias(weirdScheme, "mssql");
        schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(weirdScheme); //Should exists
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<MsSqlServerRewriter>());
    }

    [Test]
    public void AddMapping_NewScheme_CorrectType()
    {
        (var databaseName, var weirdScheme, var invariantName) = ("XY for Z", "xyz", "x.y.z");

        var builder = new SchemeMapperBuilder();
        var schemeMapper = builder.Build();
        Assert.Catch<SchemeNotFoundException>(() => schemeMapper.GetMapper(weirdScheme)); //Should not exists

        DbProviderFactories.RegisterFactory(invariantName, Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        builder.AddMapping<StubMapper, AnsiDialect, PositionalParametrizer>(databaseName, new[] { weirdScheme }, invariantName);

        schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(weirdScheme); //Should exists
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<StubMapper>());
    }

    [Test]
    public void RemoveMapping_ExistingScheme_NotFound()
    {
        var oracleScheme = "ora";

        var builder = new SchemeMapperBuilder();
        var schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(oracleScheme); //should be found
        Assert.That(result, Is.Not.Null);

        builder.RemoveMapping(oracleScheme);
        schemeMapper = builder.Build();
        Assert.Catch<SchemeNotFoundException>(() => schemeMapper.GetMapper(oracleScheme)); //Should not exist
    }

    [Test]
    [Ignore("To be re-implemented")]
    public void ReplaceMapper_NewScheme_CorrectType()
    {
        var mysqlScheme = "mysql";

        var builder = new SchemeMapperBuilder();
        if (!DbProviderFactories.GetProviderInvariantNames().Contains("MySql.Data"))
            DbProviderFactories.RegisterFactory("MySql.Data", MySql.Data.MySqlClient.MySqlClientFactory.Instance);
        builder.ReplaceMapper(typeof(MySqlConnectorMapper), typeof(MySqlDataMapper));

        var schemeMapper = builder.Build();
        var result = schemeMapper.GetMapper(mysqlScheme);
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<MySqlDataMapper>());
    }

    private class FakeDriverLocator : IDriverLocator
    {
        public FakeDriverLocator() { }

        public string Locate() => "ODBC Driver for fooBar";
    }

    [Test]
    [Ignore("To be re-implemented")]
    public void ReplaceDriverLocationFactory_NewDriverLocationFactory_CorrectType()
    {
        var factory = new DriverLocatorFactory();
        factory.AddDriver("foobar", typeof(FakeDriverLocator));

        var builder = new SchemeMapperBuilder();
        var schemeMapper = builder.Build();
        DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
        builder.ReplaceDriverLocatorFactory(typeof(OdbcRewriter), factory);

        builder.Build();
        var result = schemeMapper.GetMapper(["odbc", "foobar"]);
        Assert.That(result, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.TypeOf<OdbcRewriter>());
            Assert.That(((OdbcRewriter)result).DriverLocatorFactory, Is.EqualTo(factory));
        });
    }

    [Test]
    public void Build_TwiceTheSameAlias_Throws()
    {
        DbProviderFactories.RegisterFactory("fakedb.provider.instance", Microsoft.Data.SqlClient.SqlClientFactory.Instance);
        var builder = new SchemeMapperBuilder([]);
        builder.Initialize
        (
            [
                new MapperInfo(typeof(FakeMapper), "FakeDB", ["fake"], typeof(AnsiDialect), 0, "fakedb.provider.instance", typeof(NamedParametrizer), string.Empty, "#fff", "#000"),
                new MapperInfo(typeof(StubMapper), "StubDB", ["stub", "fake"], typeof(AnsiDialect), 0, "fakedb.provider.instance", typeof(NamedParametrizer), string.Empty, "#fff", "#000")
            ]
        );
        Assert.That(builder.Build, Throws.TypeOf<MapperAlreadyExistingException>());
    }
}
