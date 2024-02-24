using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Registering;
using NUnit.Framework;

namespace DubUrl.QA;
public class ReferencedAssembliesDiscovererTest
{
    [Test]
    [TestCase("Microsoft.Data.SqlClient.SqlClientFactory")]
    [TestCase("Npgsql.NpgsqlFactory")]
    [TestCase("Oracle.ManagedDataAccess.Client.OracleClientFactory")]
    [TestCase("MySqlConnector.MySqlConnectorFactory")]
    [TestCase("MySql.Data.MySqlClient.MySqlClientFactory")]
    [TestCase("Microsoft.Data.Sqlite.SqliteFactory")]
    [TestCase("FirebirdSql.Data.FirebirdClient.FirebirdClientFactory")]
    [TestCase("IBM.Data.Db2.DB2Factory")]
    [TestCase("Teradata.Client.Provider.TdFactory")]
    [TestCase("Snowflake.Data.Client.SnowflakeDbFactory")]
    [TestCase("DuckDB.NET.Data.DuckDBClientFactory")]
    [TestCase("System.Data.Odbc.OdbcFactory")]
    public void Execute_CurrentAssembly_IncludesType(string type)
    {
        var discover = new ReferencedAssembliesDiscoverer(GetType().Assembly);
        var types = discover.Execute();
        Assert.That(types.Select(t => t.FullName), Does.Contain(type));
    }

    [Test]
    [TestCase(typeof(DbProviderFactory))]
    public void Execute_CurrentAssembly_ExcludeType(Type type)
    {
        var discover = new ReferencedAssembliesDiscoverer(GetType().Assembly);
        var types = discover.Execute();
        Assert.That(types.Select(t => t.FullName), Does.Not.Contain(type.FullName));
    }

}
