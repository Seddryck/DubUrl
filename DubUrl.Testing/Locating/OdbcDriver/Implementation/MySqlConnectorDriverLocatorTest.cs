using DubUrl.Locating.OdbcDriver;
using DubUrl.Locating.OdbcDriver.Implementation;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation
{
    public class MySqlConnectorDriverLocatorTest
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
            var driverLister = new FakeDriverLister(new[] { "MySQL ODBC 5.3 Unicode Driver" });
            var driverLocator = new MySqlConnectorDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MySQL ODBC 5.3 Unicode Driver"));
        }

        [Test]
        public void Locate_SingleElementMatchingWhenencodingApplied_ElementReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "MySQL ODBC 5.3 ANSI Driver", "MySQL ODBC 5.3 Unicode Driver" });
            var driverLocator = new MySqlConnectorDriverLocator(driverLister, EncodingOption.Unicode);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MySQL ODBC 5.3 Unicode Driver"));
        }

        [Test]
        public void Locate_MultipleElementMatching_BestElementReturned()
        {
            var driverLister = new FakeDriverLister(new[] {
                "MySQL ODBC 5.3 ANSI Driver",
                "MySQL ODBC 5.3 Unicode Driver",
                "MySQL ODBC 8.0 ANSI Driver",
                "MySQL ODBC 8.0 Unicode Driver" });
            var driverLocator = new MySqlConnectorDriverLocator(driverLister, EncodingOption.Unicode);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MySQL ODBC 8.0 Unicode Driver"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "MySQL ODBC 5.3 Unicode Driver", "PostgreSQL ANSI" });
            var driverLocator = new MySqlConnectorDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MySQL ODBC 5.3 Unicode Driver"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "PostgreSQL ANSI" });
            var driverLocator = new MySqlConnectorDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.Null.Or.Empty);
        }
    }
}
