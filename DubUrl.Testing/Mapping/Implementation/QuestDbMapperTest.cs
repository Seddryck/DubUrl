using DubUrl.Mapping.Implementation;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
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

public class QuestDbMapperTest
{
    private const string PROVIDER_NAME = "Npgsql";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, NpgsqlFactory.Instance);
    }

    [Test]
    public void GetDialect_None_DialectReturned()
    {
        var mapper = new QuestDbMapper(ConnectionStringBuilder, new QuestDbDialect(new SqlLanguage(), new[] { "quest", "questdb" }, new PgsqlRenderer(), Array.Empty<ICaster>()), new PositionalParametrizer());
        var result = mapper.GetDialect();

        Assert.That(result, Is.Not.Null.Or.Empty);
        Assert.That(result, Is.InstanceOf<QuestDbDialect>());
        Assert.That(result.Aliases, Does.Contain("quest"));
        Assert.That(result.Aliases, Does.Contain("questdb"));
    }
}
