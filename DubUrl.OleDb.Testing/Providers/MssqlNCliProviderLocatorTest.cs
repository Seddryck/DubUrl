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
    public class MssqlNCliProviderLocatorTest
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
            var providerLister = new FakeProviderLister(new[] { new ProviderInfo("SQLNCLI11", "SQL Server Native Client 11.0") });
            var providerLocator = new MssqlNCliProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("SQLNCLI11"));
        }

        [Test]
        public void Locate_MultipleElementMatching_BestElementReturned()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("SQLNCLI10", "SQL Server Native Client 10.0"),
                    new ProviderInfo("SQLNCLI11", "SQL Server Native Client 11.0"),
                    new ProviderInfo("SQLNCLI9", "SQL Server Native Client 9.0") }
            );
            var providerLocator = new MssqlNCliProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("SQLNCLI11"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0"),
                    new ProviderInfo("SQLNCLI11", "SQL Server Native Client 11.0"),
                    new ProviderInfo("MSOLEDBSQL", "Microsoft OLE DB Driver for SQL Server") }
            );
            var providerLocator = new MssqlNCliProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("SQLNCLI11"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0") });
            var providerLocator = new MssqlNCliProviderLocator(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.Null.Or.Empty);
        }
    }
}
