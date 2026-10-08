using DubUrl.Locating.OdbcDriver;
using DubUrl.Parsing;
using DubUrl.Rewriting.Implementation;
using Moq;
using NUnit.Framework;
using System.Data.Common;
using System.Data.Odbc;

namespace DubUrl.Testing.Rewriting.Implementation;

public class PostgresqlOdbcRewriterTest
{
    private static OdbcConnectionStringBuilder ConnectionStringBuilder
        => new();

    [Test]
    public void Parse_ExplicitDriverUrl_ReturnsLinuxCompatibleConnectionString()
    {
        DbProviderFactories.RegisterFactory("System.Data.Odbc", OdbcFactory.Instance);

        var connectionString = new ConnectionUrl(
            "odbc+pgsql://user:password@localhost:5544/customers?Driver=PostgreSQL%20Unicode").Parse();
        var result = new OdbcConnectionStringBuilder(connectionString);

        Assert.Multiple(() =>
        {
            Assert.That(result[OdbcRewriter.SERVER_KEYWORD], Is.EqualTo("localhost"));
            Assert.That(result[PostgresqlOdbcRewriter.PORT_KEYWORD], Is.EqualTo("5544"));
            Assert.That(result[OdbcRewriter.DRIVER_KEYWORD], Is.EqualTo("{PostgreSQL Unicode}"));
        });
    }

    [Test]
    public void Map_ExplicitDriverAndPort_UsesPostgresqlKeywordsWithoutDiscovery()
    {
        var factory = new Mock<DriverLocatorFactory>();
        var urlInfo = new UrlInfo(
            Host: "database.example",
            Port: 5544,
            Username: "user",
            Password: "password")
        {
            Schemes = ["odbc", "pgsql"],
            Segments = ["customers"],
            Options = new Dictionary<string, string>
            {
                ["Driver"] = "PostgreSQL Unicode"
            }
        };

        var result = new PostgresqlOdbcRewriter(ConnectionStringBuilder, factory.Object).Execute(urlInfo);

        Assert.Multiple(() =>
        {
            Assert.That(result[OdbcRewriter.SERVER_KEYWORD], Is.EqualTo("database.example"));
            Assert.That(result[PostgresqlOdbcRewriter.PORT_KEYWORD], Is.EqualTo("5544"));
            Assert.That(result[OdbcRewriter.DATABASE_KEYWORD], Is.EqualTo("customers"));
            Assert.That(result[OdbcRewriter.DRIVER_KEYWORD], Is.EqualTo("{PostgreSQL Unicode}"));
        });
        factory.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
        factory.Verify(x => x.Instantiate(It.IsAny<string>(), It.IsAny<IDictionary<Type, object>>()), Times.Never);
    }

    [Test]
    public void Map_WithoutPort_OmitsPortKeyword()
    {
        var urlInfo = new UrlInfo(Host: "localhost")
        {
            Segments = ["customers"],
            Options = new Dictionary<string, string>
            {
                ["Driver"] = "PostgreSQL Unicode"
            }
        };

        var result = new PostgresqlOdbcRewriter(ConnectionStringBuilder).Execute(urlInfo);

        Assert.That(result, Does.Not.ContainKey(PostgresqlOdbcRewriter.PORT_KEYWORD));
    }
}
