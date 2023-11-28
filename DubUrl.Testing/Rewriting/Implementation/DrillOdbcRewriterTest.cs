using DubUrl.Parsing;
using DubUrl.Locating.OdbcDriver;
using DubUrl.Rewriting.Implementation;
using Moq;
using NUnit.Framework;
using System.Data.Common;
using System.Data.Odbc;

namespace DubUrl.Testing.Rewriting.Implementation;

public class DrillOdbcRewriterTest
{
    private const string PROVIDER_NAME = "System.Data.Odbc";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
    }

    [Test]
    [TestCase("host", "host")]
    [TestCase("", "localhost")]
    public void Map_UrlInfo_ReturnsServer(string host, string expected)
    {
        var urlInfo = new UrlInfo() { Host = host, Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.SERVER_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[DrillOdbcRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
            Assert.That(result[DrillOdbcRewriter.CONNECTION_KEYWORD], Is.EqualTo("Direct"));
            Assert.That(result, Does.Not.ContainKey(DrillOdbcRewriter.ZKQUORUM_KEYWORD));
        });
    }


    [Test]
    [TestCase("192.168.222.160:31010, 192.168.222.165:31010, 192.168.222.231:31010", "192.168.222.160:31010, 192.168.222.165:31010, 192.168.222.231:31010")]
    [TestCase("192.168.222.160, 192.168.222.165, 192.168.222.231", "192.168.222.160:31010, 192.168.222.165:31010, 192.168.222.231:31010")]
    public void Map_UrlInfo_ReturnsZkQuorum(string host, string expected)
    {
        var urlInfo = new UrlInfo() { Host = host, Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(DrillOdbcRewriter.SERVER_KEYWORD));
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.ZKQUORUM_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[DrillOdbcRewriter.ZKQUORUM_KEYWORD], Is.EqualTo(expected));
            Assert.That(result[DrillOdbcRewriter.CONNECTION_KEYWORD], Is.EqualTo("ZooKeeper"));
        });
    }

    [Test]
    [TestCase(0, "31010")]
    [TestCase(31010, "31010")]
    [TestCase(12345, "12345")]
    public void Map_UrlInfo_ReturnsPort(int port, string expected)
    {
        var urlInfo = new UrlInfo() { Port = port, Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.PORT_KEYWORD));
        Assert.That(result[DrillOdbcRewriter.PORT_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("dfs", "dfs")]
    [TestCase("DRILL/dfs", "dfs")]
    [TestCase("", "")]
    public void Map_UrlInfo_ReturnsSchema(string segmentsList, string expected)
    {
        var urlInfo = new UrlInfo() { Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.SCHEMA_KEYWORD));
        Assert.That(result[DrillOdbcRewriter.SCHEMA_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[DrillOdbcRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(DrillOdbcRewriter.PASSWORD_KEYWORD));
        });
        Assert.Multiple(() =>
        {
            Assert.That(result[DrillOdbcRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
            Assert.That(result, Does.ContainKey(DrillOdbcRewriter.AUTHENTICATION_KEYWORD));
        });
        Assert.That(result[DrillOdbcRewriter.AUTHENTICATION_KEYWORD], Is.EqualTo("Plain"));
    }

    [Test]
    public void Map_UrlInfoWithoutUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "", Password = "", Options = new Dictionary<string, string>() { { "Driver", "MapR Drill ODBC Driver" } } };
        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Not.ContainKey(DrillOdbcRewriter.USERNAME_KEYWORD));
        Assert.That(result, Does.Not.ContainKey(DrillOdbcRewriter.PASSWORD_KEYWORD));
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.AUTHENTICATION_KEYWORD));
        Assert.That(result[DrillOdbcRewriter.AUTHENTICATION_KEYWORD], Is.EqualTo("No Authentication"));
    }

    [Test]
    public void Map_OptionsContainsOptions_OptionsReturned()
    {
        var urlInfo = new UrlInfo() { Segments = new[] { "db" }, Schemes = new[] { "odbc", "drill", "MapR Drill ODBC Driver" } };
        urlInfo.Options.Add("ZKClusterID", "DrillBit200");

        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("ZKClusterID"));
        Assert.That(result["ZKClusterID"], Is.EqualTo("DrillBit200"));
    }

    [Test]
    public void Map_SchemeContainsDriverName_DriverNameReturned()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "drill", "{MapR Drill ODBC Driver}" }, Segments = new[] { "dfs" } };

        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder);
        var result = mapper.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(DrillOdbcRewriter.DRIVER_KEYWORD));
        Assert.That(result[DrillOdbcRewriter.DRIVER_KEYWORD], Is.EqualTo("{MapR Drill ODBC Driver}"));
    }

    [Test]
    public void Map_DriverSpecified_NoDriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "drill", "{MapR Drill ODBC Driver}" }, Segments = new[] { "dfs" } };

        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "drill", "pgsql" });
        driverLocationFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = mapper.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Map_NoDriverSpecifiedNoAdditionalOption_DriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = new[] { "odbc", "drill" }, Segments = new[] { "db" } };

        var driverLocationMock = new Mock<IDriverLocator>();
        driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "drill", "pgsql" });
        driverLocationFactoryMock.Setup(x =>
                x.Instantiate(It.IsAny<string>())
            ).Returns(driverLocationMock.Object);

        var mapper = new DrillOdbcRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = mapper.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.GetValidAliases(), Times.AtLeastOnce);
        driverLocationFactoryMock.Verify(x => x.Instantiate("drill"), Times.Once);
        driverLocationMock.Verify(x => x.Locate());
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Driver"));
        Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
    }

}
