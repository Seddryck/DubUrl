﻿using DubUrl.OleDb;
using DubUrl.OleDb.Providers;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing.Providers;

public class MssqlOleDbProviderLocatorTest
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
        var providerLister = new FakeProviderLister([new ProviderInfo("MSOLEDBSQL", "Microsoft OLE DB Driver for SQL Server")]);
        var providerLocator = new MssqlOleDbProviderLocator(providerLister);
        var provider = providerLocator.Locate();
        Assert.That(provider, Is.EqualTo("MSOLEDBSQL"));
    }

    [Test]
    public void Locate_ElementNonMatching_ElementNotReturned()
    {
        var providerLister = new FakeProviderLister(
            [ new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0"),
                new ProviderInfo("MSOLEDBSQL", "Microsoft OLE DB Driver for SQL Server"),
                new ProviderInfo("SQLNCLI11", "SQL Server Native Client 11.0") ]
        );
        var providerLocator = new MssqlOleDbProviderLocator(providerLister);
        var provider = providerLocator.Locate();
        Assert.That(provider, Is.EqualTo("MSOLEDBSQL"));
    }

    [Test]
    public void Locate_NoMatching_EmptyString()
    {
        var providerLister = new FakeProviderLister(
            [new ProviderInfo("MSOLAP", "Microsoft OLE DB Provider for Analysis Services 14.0")]);
        var providerLocator = new MssqlOleDbProviderLocator(providerLister);
        var provider = providerLocator.Locate();
        Assert.That(provider, Is.Null.Or.Empty);
    }
}
