using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using DubUrl.Mapping;
using System.Data.Common;
using DubUrl.Parsing;

namespace DubUrl.Testing.Mapping
{
    public class MapperFactoryTest
    {
        [SetUp]
        public void DefaultRegistration()
        {
            DbProviderFactories.RegisterFactory("System.Data.SqlClient", System.Data.SqlClient.SqlClientFactory.Instance);
            DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
            DbProviderFactories.RegisterFactory("MySql", MySqlConnector.MySqlConnectorFactory.Instance);
            DbProviderFactories.RegisterFactory("Oracle", Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance);
        }

        private class StubMapper : BaseMapper
        {
            public StubMapper(DbConnectionStringBuilder csb) : base(csb) { }
            public override void ExecuteSpecific(UrlInfo urlInfo) { return; }
        }

        [Test]
        [TestCase("mssql", typeof(MssqlMapper))]
        [TestCase("pgsql", typeof(PgsqlMapper))]
        [TestCase("mysql", typeof(MySqlConnectorMapper))]
        [TestCase("oracle", typeof(OracleMapper))]
        public void Instantiate_Scheme_CorrectType(string scheme, Type expected)
        {
            var factory = new MapperFactory();
            var result = factory.Instantiate(scheme);

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(expected));
        }

        [Test]
        public void AddAlias_NewScheme_CorrectType()
        {
            var weirdScheme = "xyz";

            var factory = new MapperFactory();
            Assert.Catch<SchemeNotFoundException>(() => factory.Instantiate(weirdScheme)); //Should not exists

            factory.AddAlias(weirdScheme, "mssql");
            var result = factory.Instantiate(weirdScheme); //Should exists
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<MssqlMapper>());
        }

        [Test]
        public void AddMapping_NewScheme_CorrectType()
        {
            var weirdScheme = "xyz";

            var factory = new MapperFactory();
            Assert.Catch<SchemeNotFoundException>(() => factory.Instantiate(weirdScheme)); //Should not exists

            DbProviderFactories.RegisterFactory("xyz", System.Data.SqlClient.SqlClientFactory.Instance);
            factory.AddMapping(weirdScheme, "xyz", typeof(StubMapper));
            
            var result = factory.Instantiate(weirdScheme); //Should exists
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<StubMapper>());
        }

        [Test]
        public void RemoveMapping_ExistingScheme_NotFound()
        {
            var oracleScheme = "ora";

            var factory = new MapperFactory();
            var result = factory.Instantiate(oracleScheme); //should be found
            Assert.That(result, Is.Not.Null);

            factory.RemoveMapping("Oracle");
            Assert.Catch<SchemeNotFoundException>(() => factory.Instantiate(oracleScheme)); //shouldn't be found
        }

        [Test]
        public void ReplaceMapping_NewScheme_CorrectType()
        {
            var mysqlScheme = "mysql";

            var factory = new MapperFactory();
            DbProviderFactories.RegisterFactory("MySql", MySql.Data.MySqlClient.MySqlClientFactory.Instance);
            factory.ReplaceMapping(typeof(MySqlConnectorMapper), typeof(MySqlDataMapper));

            var result = factory.Instantiate(mysqlScheme);
            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf<MySqlDataMapper>());
        }
    }
}
