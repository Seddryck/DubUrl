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
using static DubUrl.Mapping.SchemeMapperBuilder;
using Moq;

namespace DubUrl.Testing.Mapping;

public class SchemeMapperBuilderTest
{
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
        var schemeMapper = new SchemeMapperBuilder(new IgnoreNotRegisteredProvider()).Build();
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

        var builder = new SchemeMapperBuilder(new IgnoreNotRegisteredProvider());
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

        var builder = new SchemeMapperBuilder(new IgnoreNotRegisteredProvider());
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

        var builder = new SchemeMapperBuilder(new IgnoreNotRegisteredProvider());
        var schemeMapper = builder.Build();
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
        var builder = new SchemeMapperBuilder([], new IgnoreNotRegisteredProvider());
        builder.Initialize
        (
            [
                new MapperInfo(typeof(FakeMapper), "FakeDB", ["fake"], typeof(AnsiDialect), 0, "fakedb.provider.instance", typeof(NamedParametrizer), string.Empty, "#fff", "#000"),
                new MapperInfo(typeof(StubMapper), "StubDB", ["stub", "fake"], typeof(AnsiDialect), 0, "fakedb.provider.instance", typeof(NamedParametrizer), string.Empty, "#fff", "#000")
            ]
        );
        Assert.That(builder.Build, Throws.TypeOf<MapperAlreadyExistingException>());
    }

    [Test]
    public void IgnoreNotRegisteredProvider_Null_True()
        => Assert.That(new IgnoreNotRegisteredProvider().Execute(null, "name"), Is.EqualTo(true));

    [Test]
    public void SkipNotRegisteredProvider_Null_False()
        => Assert.That(new SkipNotRegisteredProvider().Execute(null, "name"), Is.EqualTo(false));

    [Test]
    public void FailNotRegisteredProvider_Null_Throws()
        => Assert.That(() => new FailNotRegisteredProvider().Execute(null, "name"), Throws.TypeOf(typeof(NotRegisteredProviderException)));


    [Test]
    public void IgnoreNotRegisteredProvider_NotNull_True()
        => Assert.That(new IgnoreNotRegisteredProvider().Execute(Mock.Of<DbProviderFactory>(), "name"), Is.EqualTo(true));

    [Test]
    public void SkipNotRegisteredProvider_NotNull_True()
        => Assert.That(new SkipNotRegisteredProvider().Execute(Mock.Of<DbProviderFactory>(), "name"), Is.EqualTo(true));

    [Test]
    public void FailNotRegisteredProvider_NotNull_True()
        => Assert.That(() => new FailNotRegisteredProvider().Execute(Mock.Of<DbProviderFactory>(), "name"), Is.EqualTo(true));

}
