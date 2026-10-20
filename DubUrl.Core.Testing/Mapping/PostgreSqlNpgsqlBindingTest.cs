using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Core.ProviderPacks;
using DubUrl.Providers;
using System.Reflection;
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
        using var resource = typeof(BuiltInProviderCatalog).Assembly.GetManifestResourceStream("DubUrl.ConnectorCatalog")!;
        var definition = ProviderPackCatalogDocument.Read(resource).Entries.Single(entry => entry.Provider == "postgresql-npgsql");
        var registry = new ProviderCatalogBuilder()
            .AddModule(AttributeProviderModule.FromConnector(definition, path => Assembly.Load(Path.GetFileNameWithoutExtension(path))))
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
