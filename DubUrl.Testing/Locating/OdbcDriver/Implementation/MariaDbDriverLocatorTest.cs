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
    public class MariaDbDriverLocatorTest
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
            var driverLister = new FakeDriverLister(new[] { "MariaDB ODBC 3.1 Driver" });
            var driverLocator = new MariaDbDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MariaDB ODBC 3.1 Driver"));
        }

        [Test]
        public void Locate_MultipleElementMatching_BestElementReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "MariaDB ODBC 3.1 Driver", "MariaDB ODBC 3.0 Driver", "MariaDB ODBC 2.5 Driver" });
            var driverLocator = new MariaDbDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MariaDB ODBC 3.1 Driver"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "MariaDB ODBC 3.1 Driver" });
            var driverLocator = new MariaDbDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("MariaDB ODBC 3.1 Driver"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 17 for Other Database" });
            var driverLocator = new MariaDbDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.Null.Or.Empty);
        }
    }
}
