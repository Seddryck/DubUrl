using DubUrl.OleDb.Querying.Reading;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing.Querying.Reading
{
    public class OleDbResourceMatcherTest
    {
        [Test]
        [TestCase(new[] { "QueryId.sql" }, "mssql", "QueryId.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.oledb.pgsql.sql" }, "mssql", "QueryId.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, "mssql", "QueryId.mssql.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql", "QueryId.oledb.sql" }, "mssql", "QueryId.oledb.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql", "QueryId.oledb.sql", "QueryId.oledb.mssql.sql" }, "mssql", "QueryId.oledb.mssql.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql", "QueryId.oledb.sql", "QueryId.odbc.mssql.sql" }, "mssql", "QueryId.oledb.sql")]
        public void Locate_NativeConnectivity_Value(string[] resources, string dialect, string expected)
        {
            var resourceMatcher = new OleDbProviderResourceMatcher(new string[] { dialect });
            Assert.That(resourceMatcher.Execute("QueryId", resources), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(new[] { "QueryId.mssql.sql", "QueryId.oledb.pgsql.sql", }, "mysql")]
        public void Locate_NativeConnectivity_Exception(string[] resources, string dialect)
        {
            var resourceMatcher = new OleDbProviderResourceMatcher(new string[] { dialect });
            Assert.That(resourceMatcher.Execute("QueryId", resources), Is.Null.Or.Empty);
        }

    }
}
