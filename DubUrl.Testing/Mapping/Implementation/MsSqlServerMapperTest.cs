using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using DubUrl.Testing.Rewriting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping.Implementation
{
    public class MsSqlServerMapperTest
    {
        private const string PROVIDER_NAME = "System.Data.SqlClient";
        
        private static DbConnectionStringBuilder ConnectionStringBuilder
        {
            get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, SqlClientFactory.Instance);
        }

        [Test]
        public void GetDialect_None_DialectReturned()
        {
            var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new MssqlDialect(new[] { "mssql", "ms" }), new NamedParametrizer());
            var result = mapper.GetDialect();

            Assert.That(result, Is.Not.Null.Or.Empty);
            Assert.That(result, Is.InstanceOf<MssqlDialect>());
            Assert.Multiple(() =>
            {
                Assert.That(result.Aliases, Does.Contain("mssql"));
                Assert.That(result.Aliases, Does.Contain("ms"));
            });
        }
    }
}
