using DubUrl.Core.Testing.Conversion;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Provider.Odbc.Mapping;
using DubUrl.Provider.Odbc.Rewriting;
using Moq;
using NUnit.Framework;
using System.Data.Odbc;

namespace DubUrl.Provider.Odbc.Tests.Rewriting;

public class PostgresqlOdbcRewriterTest
{
    [TestCase("odbc+pgsql")]
    [TestCase("odbc+postgresql")]
    [TestCase("odbc+ts")]
    [TestCase("odbc+timescale")]
    public void Parse_ExplicitDriverAndPort_UsesPostgresqlMapper(string scheme)
    {
        var registry = new ProviderCatalogBuilder().AddApplicationProviders().Build();
        var connectionString = new ConnectionUrl(
            $"{scheme}://user:password@localhost:5544/customers?Driver=PostgreSQL%20Unicode", registry).Parse();
        var result = new OdbcConnectionStringBuilder(connectionString);
        Assert.Multiple(() =>
        {
            Assert.That(result["Server"], Is.EqualTo("localhost"));
            Assert.That(result["Port"], Is.EqualTo("5544"));
            Assert.That(result["Database"], Is.EqualTo("customers"));
            Assert.That(result.Driver, Is.EqualTo("{PostgreSQL Unicode}"));
        });
    }

    [TestCase("Driver", "PostgreSQL Unicode")]
    [TestCase("driver", "PostgreSQL Unicode")]
    [TestCase("DRIVER", "{PostgreSQL Unicode}")]
    public void Map_ExplicitDriver_BypassesDiscovery(string key, string driver)
    {
        var factory = new Mock<DriverLocatorFactory>();
        var locatorFactory = factory.Object;
        factory.Invocations.Clear();
        var uri = new ConnectionUri
        {
            Host = "localhost", Port = 5544, Segments = ["customers"],
            Options = new Dictionary<string, string> { [key] = driver },
        };
        var result = new PostgresqlOdbcRewriter(new OdbcConnectionStringBuilder(), locatorFactory).Execute(uri);
        Assert.That(result["Driver"], Is.EqualTo("{PostgreSQL Unicode}"));
        factory.VerifyNoOtherCalls();
    }

    [Test]
    public void Map_WithoutPort_OmitsPortKeyword()
    {
        var uri = new ConnectionUri
        {
            Host = "localhost", Segments = ["customers"],
            Options = new Dictionary<string, string> { ["Driver"] = "PostgreSQL Unicode" },
        };
        var result = new PostgresqlOdbcRewriter(new OdbcConnectionStringBuilder()).Execute(uri);
        Assert.That(result, Does.Not.ContainKey("Port"));
    }

    [TestCase("odbc+pgsql")]
    [TestCase("odbc+timescale")]
    public void Convert_PreservesSeparatePort(string scheme)
        => ConversionTestSupport.RoundTrip(scheme,
            "Driver={PostgreSQL Unicode};Server=localhost;Port=5544;Database=customers;Uid=user;Pwd=password",
            OdbcFactory.Instance,
            csb => new PostgresqlOdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
}
