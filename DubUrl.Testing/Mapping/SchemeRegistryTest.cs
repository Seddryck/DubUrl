using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Testing.Mapping;

public class SchemeRegistryTest
{
    [SetUp]
    public void RegisterProvider()
        => DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

    [Test]
    public void Resolve_UrlWithoutOptionalValues_DoesNotReuseValuesFromPreviousResolution()
    {
        var registry = new SchemeRegistryBuilder().WithAutoDiscoveredMappings().Build();

        var first = new SqlConnectionStringBuilder(
            new ConnectionUrl("mssql://user:password@first-host/first-db?Application Name=first-app", registry).Parse()
        );
        var second = new SqlConnectionStringBuilder(
            new ConnectionUrl("mssql://second-host/second-db", registry).Parse()
        );

        Assert.Multiple(() =>
        {
            Assert.That(first.UserID, Is.EqualTo("user"));
            Assert.That(first.Password, Is.EqualTo("password"));
            Assert.That(first.ApplicationName, Is.EqualTo("first-app"));
            Assert.That(second.UserID, Is.Empty);
            Assert.That(second.Password, Is.Empty);
            Assert.That(second.ApplicationName, Is.EqualTo("Core Microsoft SqlClient Data Provider"));
        });
    }

    [Test]
    public async Task Resolve_SameProviderInParallel_ReturnsIndependentConnectionStrings()
    {
        const int resolutionCount = 100;
        var registry = new SchemeRegistryBuilder().WithAutoDiscoveredMappings().Build();

        var connectionStrings = await Task.WhenAll(
            Enumerable.Range(0, resolutionCount)
                .Select(index => Task.Run(() =>
                    new ConnectionUrl($"mssql://user-{index}:password-{index}@host-{index}/database-{index}", registry).Parse()
                ))
        );

        Assert.Multiple(() =>
        {
            for (var index = 0; index < resolutionCount; index++)
            {
                var builder = new SqlConnectionStringBuilder(connectionStrings[index]);
                Assert.That(builder.DataSource, Is.EqualTo($"host-{index}"));
                Assert.That(builder.InitialCatalog, Is.EqualTo($"database-{index}"));
                Assert.That(builder.UserID, Is.EqualTo($"user-{index}"));
                Assert.That(builder.Password, Is.EqualTo($"password-{index}"));
            }
        });
    }

    [Test]
    public void GetMapper_AliasesForSameProvider_ReturnFreshMappers()
    {
        var registry = new SchemeRegistryBuilder().WithAutoDiscoveredMappings().Build();

        var first = registry.GetMapper("mssql");
        var second = registry.GetMapper("ms");

        Assert.That(second, Is.Not.SameAs(first));
    }
}
