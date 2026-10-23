using System.Data;
using System.Data.Common;
using DubUrl.Core.Testing;
using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Mapping;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.Adbc.Tests.Mapping;

[NonParallelizable]
public class DuckdbAdbcRegistrationTest
{
    [Test]
    public void ApplicationDependencies_DiscoverSingletonWithoutExplicitRegistrationOrNativeLoading()
    {
        const string invariant = "DubUrl.Provider.DuckDb.Adbc";
        DbProviderFactories.TryGetFactory(invariant, out var previous);
        DbProviderFactories.UnregisterFactory(invariant);
        try
        {
            var registry = new ProviderCatalogBuilder().AddApplicationProviders().Build();
            Assert.That(registry.GetProviderFactory(["adbc", "duckdb"]), Is.SameAs(DuckdbAdbcFactory.Instance));
            using var connection = new ConnectionUrl("duckdb+adbc://", registry).Connect();
            Assert.That(connection.State, Is.EqualTo(ConnectionState.Closed));
        }
        finally
        {
            if (previous is not null) DbProviderFactories.RegisterFactory(invariant, previous);
        }
    }

    [Test]
    public void InstalledConnector_BindsFamilyMappingToSingleDriverFactory()
    {
        var registry = new ProviderCatalogBuilder().AddModule(ConnectorFixture.Create("duckdb-adbc")).Build();
        Assert.Multiple(() =>
        {
            Assert.That(registry.GetMapper("adbc+duckdb"), Is.TypeOf<DuckdbAdbcMapper>());
            Assert.That(registry.GetProviderFactory(["adbc", "duck"]), Is.SameAs(DuckdbAdbcFactory.Instance));
            Assert.That(registry.CanHandle("duckdb"), Is.False);
            Assert.That(registry.CanHandle("odbc+duckdb"), Is.False);
            Assert.That(new ConnectionUrl("adbc+duckdb://", registry).Parse(), Is.EqualTo("path=:memory:"));
        });
    }

    [Test]
    public void AssemblyBoundaries_KeepArrowAndNativeDiscoveryOutOfCoreAndFamily()
    {
        foreach (var assembly in new[] { typeof(ConnectionUrl).Assembly, typeof(DuckdbMapper).Assembly })
        {
            var references = assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
            Assert.That(references, Has.None.StartsWith("Apache.Arrow"));
            Assert.That(references, Does.Not.Contain("DubUrl.Adbc"));
            Assert.That(references, Does.Not.Contain("DubUrl.Provider.DuckDb.Adbc"));
        }
        Assert.That(typeof(DubUrl.Adbc.AdbcProviderFactory).Assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name), Has.None.StartsWith("DubUrl.Provider."));
    }
}
