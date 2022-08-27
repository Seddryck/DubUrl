using DubUrl.OleDb;
using DubUrl.OleDb.Providers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing.Providers
{
    public class MySqlProviderLocatorTest
    {
        private class FakeProviderLister : ProviderLister
        {
            private ProviderInfo[] Providers { get; }

            public FakeProviderLister(ProviderInfo[] providers)
                => Providers = providers;

            internal override ProviderInfo[] List() => Providers;
        }

        [Test]
        public void Locate_SingleElementMatching_ElementReturned()
        {
            var providerLister = new FakeProviderLister(new[] { new ProviderInfo("MySQL Provider", "MySQL Provider") });
            var providerLocator = new MySqlProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("MySQL Provider"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0"),
                    new ProviderInfo("MySQL Provider", "MySQL Provider"),
                    new ProviderInfo("SQLNCLI11", "SQL Server Native Client 11.0") }
            );
            var providerLocator = new MySqlProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("MySQL Provider"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0") });
            var providerLocator = new MySqlProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.Null.Or.Empty);
        }
    }
}
