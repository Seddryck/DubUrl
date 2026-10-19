using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Provider.PostgreSql.Npgsql;
using DubUrl.Mapping;
using DubUrl.Rewriting;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class PostgreSqlNpgsqlBindingTest
{
    [Test]
    public void Register_ProvidesOnlyPostgreSqlSchemesWithNpgsqlFactory()
    {
        var registry = new ProviderCatalogBuilder()
            .AddModule(new ProviderModule())
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("postgresql"), Is.TypeOf<PostgresqlMapper>());
            Assert.That(registry.GetProviderFactory(["postgresql"]), Is.SameAs(NpgsqlFactory.Instance));
            Assert.That(registry.GetMapper("pg"), Is.TypeOf<PostgresqlMapper>());
            Assert.Throws<SchemeNotFoundException>(() => registry.GetMapper("timescale"));
        });
    }
}
