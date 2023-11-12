using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using Moq;
using NUnit.Framework;

namespace DubUrl.Prql.Testing
{
    public class InlinePrqlProviderTest
    {
        private static string Normalize(string value)
        {
            var sb = new StringBuilder();
            bool isPrevious = true;
            foreach (var c in value.ToCharArray())
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!isPrevious)
                    {
                        sb.Append(' ');
                        isPrevious = true;
                    }
                }
                else
                {
                    isPrevious = false;
                    sb.Append(c);
                }
            }
            return sb.ToString().Trim();
        }

        [Test]
        public void Read_AnyDialect_SQL()
        {
            var prql = "from invoices | select order_id";
            var logger = Mock.Of<IQueryLogger>();
            var provider = new InlinePrqlProvider(prql, logger);
            var dialect = new Mock<IDialect>().Object;
            var sql = provider.Read(dialect, new NativeConnectivity());
            Assert.That(Normalize(sql), Is.EqualTo("SELECT order_id FROM invoices"));
        }

        [Test]
        public void Read_MySqlDialect_SQL()
        {
            var prql = "from employees | select `first name`";
            var logger = Mock.Of<IQueryLogger>();
            var provider = new InlinePrqlProvider(prql, logger);
            var builder = new DialectBuilder();
            builder.AddAliases<MySqlDialect>(new[] { "mysql" });
            builder.Build();
            var sql = provider.Read(builder.Get<MySqlDialect>(), new NativeConnectivity());
            Assert.That(Normalize(sql), Is.EqualTo("SELECT `first name` FROM employees"));
        }

        [Test]
        public void Read_PgSqlDialect_SQL()
        {
            var prql = "from employees | select `first name`";
            var logger = Mock.Of<IQueryLogger>();
            var provider = new InlinePrqlProvider(prql, logger);
            var builder = new DialectBuilder();
            builder.AddAliases<PgsqlDialect>(new[] { "pgsql" });
            builder.Build();
            var sql = provider.Read(builder.Get<PgsqlDialect>(), new NativeConnectivity());
            Assert.That(Normalize(sql), Is.EqualTo("SELECT \"first name\" FROM employees"));
        }
    }
}
