using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.OdbcDriver.Implementation;
using DubUrl.Locating.Options;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation;

public class TimescaleDriverLocatorTest
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
        var driverLister = new FakeDriverLister(new[] { "PostgreSQL ANSI" });
        var driverLocator = new TimescaleDriverLocator(driverLister);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("PostgreSQL ANSI"));
    }

    [Test]
    public void Locate_SingleElementMatchingWhenEncodingApplied_ElementReturned()
    {
        var driverLister = new FakeDriverLister(new[] { "PostgreSQL ANSI", "PostgreSQL Unicode" });
        var driverLocator = new TimescaleDriverLocator(driverLister, EncodingOption.Unicode, ArchitectureOption.x86);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("PostgreSQL Unicode"));
    }

    [Test]
    public void Locate_MultipleElementMatching_BestElementReturned()
    {
        var driverLister = new FakeDriverLister(new[] {
            "PostgreSQL ANSI",
            "PostgreSQL Unicode",
            "PostgreSQL ANSI(x64)",
            "PostgreSQL Unicode(x64)" });
        var driverLocator = new TimescaleDriverLocator(driverLister, EncodingOption.Unicode, ArchitectureOption.x64);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("PostgreSQL Unicode(x64)"));
    }

    [Test]
    public void Locate_ElementNonMatching_ElementNotReturned()
    {
        var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "MySQL ODBC 5.3 Unicode Driver", "PostgreSQL ANSI" });
        var driverLocator = new TimescaleDriverLocator(driverLister, EncodingOption.ANSI, ArchitectureOption.x86);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.EqualTo("PostgreSQL ANSI"));
    }

    [Test]
    public void Locate_NoMatching_EmptyString()
    {
        var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "MySQL ODBC 5.3 Unicode Driver" });
        var driverLocator = new TimescaleDriverLocator(driverLister, EncodingOption.Unicode, ArchitectureOption.x64);
        var driver = driverLocator.Locate();
        Assert.That(driver, Is.Null.Or.Empty);
    }
}
