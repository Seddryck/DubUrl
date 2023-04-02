using DubUrl.Registering;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Registering
{
    public class ReferencedAssembliesDiscoverTest
    {
        [Test]

        [TestCase(typeof(System.Data.SqlClient.SqlClientFactory))]
        [TestCase(typeof(Npgsql.NpgsqlFactory))]
        [TestCase(typeof(Oracle.ManagedDataAccess.Client.OracleClientFactory))]
        [TestCase(typeof(MySqlConnector.MySqlConnectorFactory))]
        [TestCase(typeof(MySql.Data.MySqlClient.MySqlClientFactory))]
        [TestCase(typeof(Microsoft.Data.Sqlite.SqliteFactory))]
        [TestCase(typeof(FirebirdSql.Data.FirebirdClient.FirebirdClientFactory))]
        [TestCase(typeof(IBM.Data.Db2.DB2Factory))]
        [TestCase(typeof(Teradata.Client.Provider.TdFactory))]
        [TestCase(typeof(Snowflake.Data.Client.SnowflakeDbFactory))]
        [TestCase(typeof(DuckDB.NET.Data.DuckDBClientFactory))] 
        [TestCase(typeof(System.Data.Odbc.OdbcFactory))]
        public void Execute_CurrentAssembly_IncludesType(Type type)
        {
            var discover = new ReferencedAssembliesDiscover(GetType().Assembly);
            var types = discover.Execute();
            Assert.That(types.Select(t => t.FullName), Does.Contain(type.FullName));
        }

        [Test]

        [TestCase(typeof(System.Data.Common.DbProviderFactory))]
        public void Execute_CurrentAssembly_ExcludeType(Type type)
        {
            var discover = new ReferencedAssembliesDiscover(GetType().Assembly);
            var types = discover.Execute();
            Assert.That(types.Select(t => t.FullName), Does.Not.Contain(type.FullName));
        }

        [Test]
        public void Execute_CurrentAssembly_DoesntLoadMoreAssemblies()
        {
            var countLoaded = AppDomain.CurrentDomain.GetAssemblies().Count();
            var discover = new ReferencedAssembliesDiscover(GetType().Assembly);
            var types = discover.Execute();
            Assert.That(countLoaded, Is.EqualTo(AppDomain.CurrentDomain.GetAssemblies().Count()));
        }
    }
}
