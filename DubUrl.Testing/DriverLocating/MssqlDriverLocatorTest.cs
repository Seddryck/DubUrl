using DubUrl.Locating.OdbcDriver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.DriverLocating
{
    public class MssqlDriverLocatorTest
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
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server" });
            var driverLocator = new MssqlDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("ODBC Driver 13 for SQL Server"));
        }

        [Test]
        public void Locate_MultipleElementMatching_BestElementReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "ODBC Driver 17 for SQL Server", "ODBC Driver 14 for SQL Server" });
            var driverLocator = new MssqlDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("ODBC Driver 17 for SQL Server"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "ODBC Driver 17 for Other Database" });
            var driverLocator = new MssqlDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("ODBC Driver 13 for SQL Server"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 17 for Other Database" });
            var driverLocator = new MssqlDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.Null.Or.Empty);
        }
    }
}
