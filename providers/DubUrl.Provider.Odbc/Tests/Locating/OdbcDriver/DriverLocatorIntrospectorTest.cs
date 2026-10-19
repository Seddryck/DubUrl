using DubUrl.Locating.OdbcDriver;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Locating.OdbcDriver;

public class DriverLocatorIntrospectorTest
{
    [Test]
    public void Locate_RealTypes_BrandsAssociated()
    {
        var introspector = new DriverLocatorIntrospector();
        var result = introspector.Locate();
        Assert.Multiple(() =>
        {
            Assert.That(result.Any(x => !string.IsNullOrEmpty(x.Slug)), Is.True);
            Assert.That(result.All(x => !string.IsNullOrEmpty(x.MainColor)), Is.True);
            Assert.That(result.All(x => !string.IsNullOrEmpty(x.SecondaryColor)), Is.True);
        });
    }
}
