using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.OdbcDriver.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation;

public class DuckdbDriverLocatorTest
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
        var driverLister = new FakeDriverLister(["DuckDB Driver"]);
        var driverLocator = new DuckdbDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("DuckDB Driver"));
    }

    [Test]
    public void Locate_ElementNonMatching_ElementNotReturned()
    {
        var driverLister = new FakeDriverLister(["DuckDB Driver", "ODBC Driver 17 for Other Database"]);
        var driverLocator = new DuckdbDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("DuckDB Driver"));
    }

    [Test]
    public void Locate_NoMatching_EmptyString()
    {
        var driverLister = new FakeDriverLister(["ODBC Driver 17 for Other Database"]);
        var driverLocator = new DuckdbDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.Null.Or.Empty);
    }
}
