using DubUrl.Parsing;
using NUnit.Framework;
using System.Data.Common;
using System.Data.Odbc;
using DubUrl.Locating.OdbcDriver;
using Moq;
using DubUrl.Rewriting.Implementation;

namespace DubUrl.QA.Odbc;

[Category("ODBC")]
[Category("ConnectionString")]
public class OdbcDbqRewriterTest
{
    private const string PROVIDER_NAME = "System.Data.Odbc";

    private static DbConnectionStringBuilder ConnectionStringBuilder
    {
        get => ConnectionStringBuilderHelper.Retrieve(PROVIDER_NAME, OdbcFactory.Instance);
    }

    [Test]
    [TestCase("sheet.xlsx", "")]
    [TestCase("localhost", "sheet.xlsx")]
    [TestCase(".", "sheet.xlsx")]
    [TestCase("", "sheet.xlsx")]
    public void Map_DoubleSlash_Dbq(string host, string segmentsList, string expected = "sheet.xlsx")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcDbqRewriter.SERVER_KEYWORD));
        Assert.That(result[OdbcDbqRewriter.SERVER_KEYWORD], Is.EqualTo(expected));
    }

    [Test]
    [TestCase("localhost", "directory/sheet.xlsx")]
    [TestCase(".", "directory/sheet.xlsx")]
    [TestCase("", "directory/sheet.xlsx")]
    public void Map_TripleSlash_Dbq(string host, string segmentsList, string expected = "directory/sheet.xlsx")
    {
        var urlInfo = new UrlInfo() { Host = host, Segments = segmentsList.Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcDbqRewriter.SERVER_KEYWORD));
        Assert.That(result[OdbcDbqRewriter.SERVER_KEYWORD], Is.EqualTo(expected.Replace('/', Path.DirectorySeparatorChar)));
    }


    [Test]
    public void Map_QuadrupleSlash_DataSource()
    {
        var path = "c:/directory/sheet.xlsx";
        var urlInfo = new UrlInfo() { Host = string.Empty, Segments = $"//{path}".Split('/'), Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcDbqRewriter.SERVER_KEYWORD));
        Assert.That(result[OdbcDbqRewriter.SERVER_KEYWORD], Is.EqualTo(path.Replace('/', Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Map_UrlInfoWithUsernamePassword_Authentication()
    {
        var urlInfo = new UrlInfo() { Username = "user", Password = "pwd", Segments = ["db"], Options = new Dictionary<string, string>() { { "Driver", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" } } };
        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcDbqRewriter.USERNAME_KEYWORD));
        Assert.Multiple(() =>
        {
            Assert.That(result[OdbcDbqRewriter.USERNAME_KEYWORD], Is.EqualTo("user"));
            Assert.That(result, Does.ContainKey(OdbcDbqRewriter.PASSWORD_KEYWORD));
        });
        Assert.That(result[OdbcDbqRewriter.PASSWORD_KEYWORD], Is.EqualTo("pwd"));
    }

    [Test]
    public void Map_OptionsContainsOptions_OptionsReturned()
    {
        var urlInfo = new UrlInfo() { Segments = ["db"], Schemes = ["odbc", "xlsx", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)"] };
        urlInfo.Options.Add("sslmode", "required");
        urlInfo.Options.Add("charset", "UTF8");

        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("sslmode"));
        Assert.Multiple(() =>
        {
            Assert.That(result["sslmode"], Is.EqualTo("required"));
            Assert.That(result, Does.ContainKey("charset"));
        });
        Assert.That(result["charset"], Is.EqualTo("UTF8"));
    }

    [Test]
    public void Map_SchemeContainsDriverName_DriverNameReturned()
    {
        var urlInfo = new UrlInfo() { Schemes = ["odbc", "xlsx", "{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}"], Segments = ["db"] };

        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder);
        var result = Rewriter.Execute(urlInfo);

        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey(OdbcDbqRewriter.DRIVER_KEYWORD));
        Assert.That(result[OdbcDbqRewriter.DRIVER_KEYWORD], Is.EqualTo("{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}"));
    }

    [Test]
    public void Map_DriverSpecified_NoDriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = ["odbc", "xlsx", "{Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)}"], Segments = ["db"] };

        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "odbc+xlsx", "xlsx" });
        driverLocationFactoryMock.Setup(x => x.Instantiate(It.IsAny<string>()));

        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = Rewriter.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.Instantiate(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Map_NoDriverSpecifiedNoAdditionalOption_DriverLocationCalled()
    {
        var urlInfo = new UrlInfo() { Schemes = ["odbc", "xlsx"], Segments = ["sheet.xlsx"] };

        var driverLocationMock = new Mock<IDriverLocator>();
        driverLocationMock.Setup(x => x.Locate()).Returns("My driver");
        var driverLocationFactoryMock = new Mock<DriverLocatorFactory>();
        driverLocationFactoryMock.Setup(x => x.GetValidAliases()).Returns(new[] { "xlsx", "odbc+xlsx" });
        driverLocationFactoryMock.Setup(x =>
                x.Instantiate(It.IsAny<string>())
            ).Returns(driverLocationMock.Object);

        var Rewriter = new OdbcDbqRewriter(ConnectionStringBuilder, driverLocationFactoryMock.Object);
        var result = Rewriter.Execute(urlInfo);

        driverLocationFactoryMock.Verify(x => x.GetValidAliases(), Times.AtLeastOnce);
        driverLocationFactoryMock.Verify(x => x.Instantiate("xlsx"), Times.Once);
        driverLocationMock.Verify(x => x.Locate());
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.ContainKey("Driver"));
        Assert.That(result["Driver"], Is.EqualTo("{My driver}"));
    }

}
