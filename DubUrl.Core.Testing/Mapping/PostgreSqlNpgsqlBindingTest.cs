using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Core.ProviderPacks;

using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Rewriting;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing.Mapping;

public class PostgreSqlNpgsqlBindingTest
{
    [Test]
    public void Register_ProvidesEveryPostgreSqlFamilyMappingWithNpgsqlFactory()
    {
        var registry = new ProviderCatalogBuilder()
            .AddModule(ConnectorFixture.Create("postgresql-npgsql"))
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("postgresql"), Is.TypeOf<PostgresqlMapper>());
            Assert.That(registry.GetProviderFactory(["postgresql"]), Is.SameAs(NpgsqlFactory.Instance));
            Assert.That(registry.GetMapper("pg"), Is.TypeOf<PostgresqlMapper>());
            Assert.That(registry.GetMapper("timescale").GetType().Name, Is.EqualTo("TimescaleMapper"));
            Assert.That(registry.GetMapper("cockroach").GetType().Name, Is.EqualTo("CockRoachMapper"));
        });
    }
}
