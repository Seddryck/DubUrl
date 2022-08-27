using DubUrl.OleDb;
using DubUrl.OleDb.Providers;
using DubUrl.Mapping.Tokening;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing.Providers
{
    public class AceProviderLocatorTest
    {
        private class FakeProviderLister : ProviderLister
        {
            private ProviderInfo[] Providers { get; }

            public FakeProviderLister(ProviderInfo[] providers)
                => Providers = providers;

            internal override ProviderInfo[] List() => Providers;
        }

        private class AceProviderLocatorTestable : AceProviderLocator
        {
            public AceProviderLocatorTestable(ProviderLister providerLister)
            : base(providerLister) { }
        }

        [Test]
        public void Locate_SingleElementMatching_ElementReturned()
        {
            var providerLister = new FakeProviderLister(new[] { new ProviderInfo("Microsoft.ACE.OLEDB.16.0", "Microsoft Office 16.0 Access Database Engine OLE DB Provider") });
            var providerLocator = new AceProviderLocatorTestable(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("Microsoft.ACE.OLEDB.16.0"));
        }

        [Test]
        public void Locate_MultipleElementMatching_BestElementReturned()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("Microsoft.ACE.OLEDB.12.0", "Microsoft Office 12.0 Access Database Engine OLE DB Provider"),
                    new ProviderInfo("Microsoft.ACE.OLEDB.16.0", "Microsoft Office 16.0 Access Database Engine OLE DB Provider"),
                    new ProviderInfo("Microsoft.ACE.OLEDB.7.0", "Microsoft Office 7.0 Access Database Engine OLE DB Provider") }
            );
            var providerLocator = new AceProviderLocatorTestable(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("Microsoft.ACE.OLEDB.16.0"));
        }

        [Test]
        public void Locate_ElementNonMatching_ElementNotReturned()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0"),
                    new ProviderInfo("Microsoft.ACE.OLEDB.16.0", "Microsoft Office 16.0 Access Database Engine OLE DB Provider"),
                    new ProviderInfo("MSOLEDBSQL", "Microsoft OLE DB Driver for SQL Server") }
            );
            var providerLocator = new AceProviderLocatorTestable(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.EqualTo("Microsoft.ACE.OLEDB.16.0"));
        }

        [Test]
        public void Locate_NoMatching_EmptyString()
        {
            var providerLister = new FakeProviderLister(
                new[] { new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0") });
            var providerLocator = new AceProviderLocatorTestable(providerLister);
            var provider = providerLocator.Locate();
            Assert.That(provider, Is.Null.Or.Empty);
        }


        [Test]
        [TestCase(typeof(AceXlsProviderLocator))]
        [TestCase(typeof(AceXlsxProviderLocator))]
        [TestCase(typeof(AceXlsmProviderLocator))]
        [TestCase(typeof(AceXlsbProviderLocator))]
        public void OptionsMapper_ExtendedProperties(Type aceProviderLocatorType)
        {
            var aceProviderLocator = Activator.CreateInstance(aceProviderLocatorType) as AceProviderLocator ?? throw new InvalidCastException();
            Assert.That(aceProviderLocator.OptionsMapper, Is.TypeOf<ExtendedPropertiesMapper>());
        }
    }
}
