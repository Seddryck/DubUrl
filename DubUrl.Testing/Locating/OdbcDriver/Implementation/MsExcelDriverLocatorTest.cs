using DubUrl.Locating.OdbcDriver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation;

public class MsExcelDriverLocatorTest
{
    private class FakeDriverLister : DriverLister
    {
        private string[] Drivers { get; }

        public FakeDriverLister(string[] drivers)
            => Drivers = drivers;

        public override string[] List() => Drivers;
    }

    [Test]
    public void Locate_SingleElementMatching_ElementReturned()
    {
        var driverLister = new FakeDriverLister(new[] { "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" });
        var driverLocator = new MsExcelDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)"));
    }

    [Test]
    public void Locate_MultipleIdenticalElementMatching_BestElementReturned()
    {
        var driverLister = new FakeDriverLister(new[] { "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" });
        var driverLocator = new MsExcelDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)"));
    }

    [Test]
    public void Locate_ElementNonMatching_ElementNotReturned()
    {
        var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" });
        var driverLocator = new MsExcelDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)"));
    }

    [Test]
    public void Locate_NoMatching_EmptyString()
    {
        var driverLister = new FakeDriverLister(new[] { "ODBC Driver 17 for Other Database" });
        var driverLocator = new MsExcelDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.Null.Or.Empty);
    }
}
