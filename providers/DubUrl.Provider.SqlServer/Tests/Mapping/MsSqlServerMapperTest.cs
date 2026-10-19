using DubUrl.Provider.SqlServer.Mapping;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Provider.SqlServer.Sql.Functions;
using DubUrl.Provider.SqlServer.Sql.Rendering;
using DubUrl.Provider.SqlServer.Sql.Types;
using DubUrl.Testing;
using DubUrl.Testing.Mapping;
using DubUrl.Querying.Dialects;
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
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.TypeMapping;
using DubUrl.Querying.Dialects.Functions;

namespace DubUrl.Provider.SqlServer.Tests.Mapping;

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
        var mapper = new MsSqlServerMapper(ConnectionStringBuilder, new TSqlDialect(new SqlLanguage(), ["mssql", "ms"], new TSqlRenderer(), [], TSqlTypeMapper.Instance, TSqlFunctionMapper.Instance), new NamedParametrizer());
        var result = mapper.GetDialect();

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<TSqlDialect>());
        Assert.Multiple(() =>
        {
            Assert.That(result.Aliases, Does.Contain("mssql"));
            Assert.That(result.Aliases, Does.Contain("ms"));
        });
    }
}
