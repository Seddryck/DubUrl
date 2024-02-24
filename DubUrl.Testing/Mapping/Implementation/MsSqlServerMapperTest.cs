using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using DubUrl.Testing.Rewriting;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Casters;
using Microsoft.Data.SqlClient;

namespace DubUrl.Testing.Mapping.Implementation;

public class MsSqlServerMapperTest
{
    [Test]
    public void GetDialect_None_DialectReturned()
    {
        var mapper = new MsSqlServerMapper([], new TSqlDialect(new SqlLanguage(), ["mssql", "ms"], new TSqlRenderer(), []), new NamedParametrizer());
        var result = mapper.GetDialect();

        Assert.That(result, Is.Not.Null.Or.Empty);
        Assert.That(result, Is.InstanceOf<TSqlDialect>());
        Assert.Multiple(() =>
        {
            Assert.That(result.Aliases, Does.Contain("mssql"));
            Assert.That(result.Aliases, Does.Contain("ms"));
        });
    }
}
