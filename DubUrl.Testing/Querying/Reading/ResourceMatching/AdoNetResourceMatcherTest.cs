using DubUrl.Mapping.Connectivity;
using DubUrl.Querying;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading;
using DubUrl.Querying.Reading.ResourceManagement;
using DubUrl.Querying.Reading.ResourceMatching;
using Moq;
using MySqlX.XDevAPI;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Querying.Reading
{
    public class AdoNetResourceMatcherTest
    {
        [Test]
        [TestCase(new[] { "QueryId.sql" }, "mssql", "QueryId.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql" }, "mssql", "QueryId.mssql.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql", "QueryId.pgsql.sql", }, "pgsql", "QueryId.pgsql.sql")]
        [TestCase(new[] { "QueryId.sql", "QueryId.mssql.sql", "QueryId.pgsql.sql", }, "mysql", "QueryId.sql")]
        public void Locate_NativeConnectivity_Value(string[] resources, string dialect, string expected)
        {
            var resourceMatcher = new AdoNetResourceMatcher(new string[] { dialect });
            Assert.That(resourceMatcher.Execute("QueryId", resources), Is.EqualTo(expected));
        }

        [Test]
        [TestCase(new[] { "QueryId.mssql.sql", "QueryId.pgsql.sql", }, "mysql")]
        public void Locate_NativeConnectivity_Exception(string[] resources, string dialect)
        {
            var resourceMatcher = new AdoNetResourceMatcher(new string[] { dialect });
            Assert.That(resourceMatcher.Execute("QueryId", resources), Is.Null.Or.Empty);
        }

    }
}
