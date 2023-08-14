using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing
{
    public class ProviderLocatorIntrospectorTest
    {
        [Test]
        public void Locate_RealTypes_BrandsAssociated()
        {
            var introspector = new ProviderLocatorIntrospector();
            var result = introspector.Locate();
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Count(), Is.GreaterThan(0));
            Assert.Multiple(() =>
            {
                Assert.That(result.Any(x => !string.IsNullOrEmpty(x.Slug)), Is.True);
                Assert.That(result.All(x => !string.IsNullOrEmpty(x.MainColor)), Is.True);
                Assert.That(result.All(x => !string.IsNullOrEmpty(x.SecondaryColor)), Is.True);
            });
        }
    }
}
