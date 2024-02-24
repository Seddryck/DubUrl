using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Parametrizing;
using DubUrl.Testing.Rewriting;
using Npgsql;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping.Implementation;

public class PostgresqlMapperTest
{
    [Test]
    public void GetDialect_None_DialectReturned()
    {
        var mapper = new PostgresqlMapper([], new PgsqlDialect(new SqlLanguage(), ["pgsql", "pg"], new PgsqlRenderer(), []), new PositionalParametrizer());
        var result = mapper.GetDialect();

        Assert.That(result, Is.Not.Null.Or.Empty);
        Assert.That(result, Is.InstanceOf<PgsqlDialect>());
        Assert.Multiple(() =>
        {
            Assert.That(result.Aliases, Does.Contain("pgsql"));
            Assert.That(result.Aliases, Does.Contain("pg"));
        });
    }
}
