using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Testing.Mapping.Connectivity;
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

        [Test]
        public void Read_AnyExistingResources_InvokeCompiler()
        {
            var dialectMock = new Mock<IDialect>();
            dialectMock.SetupGet(x => x.Aliases).Returns(new[] { "mssql" });

            var connectivityMock = new Mock<IConnectivity>();
            connectivityMock.SetupGet(x => x.Alias).Returns(string.Empty);

            var queryLoggerMock = new Mock<IQueryLogger>();

            var compilerMock = new Mock<IPrqlCompiler>();
            compilerMock.Setup(x => x.ToSql(It.IsAny<string>(), It.IsAny<IDialect>())).Returns("select foo.bar");

            var query = new InlinePrqlProvider("select 'bar'", queryLoggerMock.Object, compilerMock.Object);
            var result = query.Read(dialectMock.Object, connectivityMock.Object);

            compilerMock.Verify(c => c.ToSql("select 'bar'", dialectMock.Object));
        }
    }
}
