using DubUrl.Parsing;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.Options;
using DubUrl.Rewriting.Implementation;
using Moq;
using NUnit.Framework;
using System.Data.Common;
using System.Data.Odbc;

namespace DubUrl.Testing.Rewriting.Implementation;

public class OdbcRewriterTest
{
    private const string PROVIDER_NAME = "System.Data.Odbc";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
    }

    [Test]
    [TestCase("host", "host")]
    [TestCase("host\\instance", "host", "instance/db")]
    [TestCase("host, 1234", "host", "db", 1234)]
    [TestCase("host\\instance, 1234", "host", "instance/db", 1234)]
    public void Map_UrlInfo_ReturnsServer(string expected, string host = "host", string segmentsList = "db", int port = 0)
    {
        var urlInfo = new UrlInfo() { Host = host, Port = port, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new OdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.SERVER_KEYWORD));
        Assert.That(result[OdbcRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("db")]
    public void Map_UrlInfo_ReturnsInitialCatalog(string segmentsList = "db", string expected = "db")
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new OdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.DATABASE_KEYWORD));
        Assert.That(result[OdbcRewriter.DATABASE_KEYWORD], Is.EqualTo(expected));
    }


    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = new[] { "db" }, Options = new Dictionary<string, string>() { { "Driver", "ODBC Driver 18 for SQL Server" } } };
        var mapper = new OdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.USERNAME_KEYWORD));
        Assert.That(result[OdbcRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
        Assert.That(result, Does.ContainKey(OdbcRewriter.PASSWORD_KEYWORD));
        Assert.That(result[OdbcRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
    }

    [Test]
    public void Map_OptionsContainsOptions_OptionsReturned()
    {
        var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Schemes = new[] { "odbc", "mssql", "ODBC Driver 18 for SQL Server" } };
        urlInfo.Options.Add("sslmode", "required");
        urlInfo.Options.Add("charset", "UTF8");

        var mapper = new OdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("sslmode"));
        Assert.That(result["sslmode"], Is.EqualTo("required"));
        Assert.That(result, Does.ContainKey("charset"));
        Assert.That(result["charset"], Is.EqualTo("UTF8"));
    }

    [Test]
    public void Map_SchemeContainsDriverName_DriverNameReturned()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "mssql", "{ODBC Driver 18 for SQL Server}" }, Segments = new[] { "db" } };

        var mapper = new OdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcRewriter.DRIVER_KEYWORD));
        Assert.That(result[OdbcRewriter.DRIVER_KEYWORD], Is.EqualTo("{ODBC Driver 18 for SQL Server}"));
    }

    [Test]
    public void Map_DriverSpecified_NoDriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "mssql", "{ODBC Driver 18 for SQL Server}" }, Segments = new[] { "db" } };

        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "mssql", "pgsql" });
        driverLocationFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

        var mapper = new OdbcRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = mapper.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Map_NoDriverSpecifiedNoAdditionalOption_DriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "mssql" }, Segments = new[] { "db" } };

        var driverLocationMock = new Mock<IDriverLocator>();
        driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "mssql", "pgsql" });
        driverLocationFactoryMock.Setup(x =>
                x.Instantiate(It.IsAny<string>())
            ).Returns(driverLocationMock.Object);

        var mapper = new OdbcRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = mapper.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.GetValidAliases(), Times.AtLeastOnce);
        driverLocationFactoryMock.Verify(x => x.Instantiate("mssql"), Times.Once);
        driverLocationMock.Verify(x => x.Locate());
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Driver"));
        Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
    }

    [Test, Pairwise]
    public void Map_NoDriverSpecifiedButOptions_DriverLocationCalled(
            [Values(ArchitectureOption.x64, ArchitectureOption.x86)] ArchitectureOption architecture,
            [Values(EncodingOption.ANSI, EncodingOption.Unicode)] EncodingOption encoding
        )
    {
        var schemes = new List<string>() { "odbc", "mssql" };
        schemes.Add(Enum.GetName(typeof(ArchitectureOption), architecture) ?? throw new ArgumentNullException());
        schemes.Add(Enum.GetName(typeof(EncodingOption), encoding) ?? throw new ArgumentNullException());

        var urlInfo = new UrlInfo() { Schemes = schemes.ToArray(), Segments = new[] { "db" } };

        var driverLocationMock = new Mock<IDriverLocator>();
        driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "mssql", "pgsql" });
        driverLocationFactoryMock.Setup(x =>
                x.Instantiate(It.IsAny<string>(), It.IsAny<IDictionary<Type, object>>())
            ).Returns(driverLocationMock.Object);

        var mapper = new OdbcRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = mapper.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.GetValidAliases(), Times.AtLeastOnce);
        driverLocationFactoryMock.Verify(
            x => x.Instantiate("mssql", It.Is<IDictionary<Type, object>>(
                x => x.ContainsKey(typeof(ArchitectureOption))
                    && (ArchitectureOption)x[typeof(ArchitectureOption)] == architecture
                    && x.ContainsKey(typeof(EncodingOption))
                    && (EncodingOption)x[typeof(EncodingOption)] == encoding
            )), Times.Once);
        driverLocationMock.Verify(x => x.Locate());
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Driver"));
        Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
    }
}
