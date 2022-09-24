using DubUrl.Locating.OdbcDriver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver.Implementation
{
    public class TextDriverLocatorTest
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
            var driverLister = new FakeDriverLister(new[] { "Microsoft Access Text Driver (*.txt, *.csv)" });
            var driverLocator = new TextDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("Microsoft Access Text Driver (*.txt, *.csv)"));
        }

        [Test]
        public void Locate_MultipleIdenticalElementMatching_BestElementReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "Microsoft Access Text Driver (*.txt, *.csv)", "Microsoft Excel Driver (*.xls, *.xlsx, *.xlsm, *.xlsb)" });
            var driverLocator = new TextDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("Microsoft Access Text Driver (*.txt, *.csv)"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 13 for SQL Server", "Microsoft Access Text Driver (*.txt, *.csv)" });
            var driverLocator = new TextDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.EqualTo("Microsoft Access Text Driver (*.txt, *.csv)"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var driverLister = new FakeDriverLister(new[] { "ODBC Driver 17 for Other Database" });
            var driverLocator = new TextDriverLocator(driverLister);
            var driver = driverLocator.Locate();
            Assert.That(driver, Is.Null.Or.Empty);
        }
    }
}
