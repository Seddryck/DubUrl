using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.OdbcDriver.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation;

public class TrinoDriverLocatorTest
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
        var driverLister = new FakeDriverLister(new[] { "Simba Trino ODBC Driver" });
        var driverLocator = new TrinoDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("Simba Trino ODBC Driver"));
    }

    [Test]
    public void Locate_NoMatching_EmptyString()
    {
        var driverLister = new FakeDriverLister(new[] { "ODBC Driver 17 for Other Database" });
        var driverLocator = new TrinoDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.Null.Or.Empty);
    }
}
