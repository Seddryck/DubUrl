using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using DubUrl.Mapping;
using System.Data.Common;
using DubUrl.Parsing;
using DubUrl.DriverLocating;
using Moq;

namespace DubUrl.Testing.Mapping
{
    public class SchemeMapperBuilderTest
    {
        [SetUp]
        public void DefaultRegistration()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
            DbProviderFactories.RegisterFactory("MySql", MySqlConnector.MySqlConnectorFactory.Instance);
            DbProviderFactories.RegisterFactory("Oracle", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
        }

        private class StubMapper : BaseMapper
        {
            public StubMapper(DbConnectionStringBuilder csb) : base(csb, new Specificator(csb), Array.Empty<BaseTokenMapper>()) { }
        }

        [Test]
        [TestCase("mssql", typeof(MssqlMapper))]
        [TestCase("pgsql", typeof(PgsqlMapper))]
        [TestCase("mysql", typeof(MySqlConnectorMapper))]
        [TestCase("oracle", typeof(OracleMapper))]
        [TestCase("odbc", typeof(OdbcMapper))]
        [TestCase("odbc+mssql", typeof(OdbcMapper))]
        [TestCase("mssql+odbc", typeof(OdbcMapper))]
        public void Instantiate_Scheme_CorrectType(string schemeList, Type expected)
        {
            var builder = new SchemeMapperBuilder();
            builder.Build(schemeList.Split("+"));
            var result = builder.GetMapper();

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(expected));
        }

        [Test]
        public void AddAlias_NewScheme_CorrectType()
        {
            var weirdScheme = "xyz";

            var builder = new SchemeMapperBuilder();
            Assert.Catch<SchemeNotFoundException>(() => builder.Build(weirdScheme)); //Should not exists
            Assert.Catch<InvalidOperationException>(() => builder.GetMapper()); //Should not exists

            builder.AddAlias(weirdScheme, "mssql");
            builder.Build(weirdScheme);
            var result = builder.GetMapper(); //Should exists
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<MssqlMapper>());
        }

        [Test]
        public void AddMapping_NewScheme_CorrectType()
        {
            var weirdScheme = "xyz";

            var builder = new SchemeMapperBuilder();
            Assert.Catch<SchemeNotFoundException>(() => builder.Build(weirdScheme)); //Should not exists
            Assert.Catch<InvalidOperationException>(() => builder.GetMapper()); //Should not exists

            DbProviderFactories.RegisterFactory("xyz", System.Data.SqlClient.SqlClientFactory.Instance);
            builder.AddMapping(weirdScheme, "xyz", typeof(StubMapper));

            builder.Build(weirdScheme);
            var result = builder.GetMapper(); //Should exists
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<StubMapper>());
        }

        [Test]
        public void RemoveMapping_ExistingScheme_NotFound()
        {
            var oracleScheme = "ora";

            var builder = new SchemeMapperBuilder();
            builder.Build(oracleScheme);
            var result = builder.GetMapper(); //should be found
            Assert.That(result, Is.Not.Null);

            builder.RemoveMapping("Oracle");
            Assert.Catch<SchemeNotFoundException>(() => builder.Build(oracleScheme)); //shouldn't be found
            Assert.Catch<InvalidOperationException>(() => builder.GetMapper()); //Should not exist
        }

        [Test]
        public void ReplaceMapper_NewScheme_CorrectType()
        {
            var mysqlScheme = "mysql";

            var builder = new SchemeMapperBuilder();
            DbProviderFactories.RegisterFactory("MySql", MySql.Data.MySqlClient.MySqlClientFactory.Instance);
            builder.ReplaceMapper(typeof(MySqlConnectorMapper), typeof(MySqlDataMapper));

            builder.Build(mysqlScheme);
            var result = builder.GetMapper();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<MySqlDataMapper>());
        }

        private class FakeDriverLocator : IDriverLocator
        {
            public FakeDriverLocator() { }

            public string Locate() => "ODBC Driver for fooBar";
        }

        [Test]
        public void ReplaceDriverLocationFactory_NewDriverLocationFactory_CorrectType()
        {
            var factory = new DriverLocatorFactory();
            factory.AddDriverLocator("foobar", typeof(FakeDriverLocator));

            var builder = new SchemeMapperBuilder();
            DbProviderFactories.RegisterFactory("System.Data.Odbc", System.Data.Odbc.OdbcFactory.Instance);
            builder.ReplaceDriverLocatorFactory(typeof(OdbcMapper), factory);

            builder.Build(new[] { "odbc", "foobar" });
            var result = builder.GetMapper();
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<OdbcMapper>());
            Assert.That(((OdbcMapper)result).DriverLocatorFactory, Is.EqualTo(factory));
        }
    }
}
