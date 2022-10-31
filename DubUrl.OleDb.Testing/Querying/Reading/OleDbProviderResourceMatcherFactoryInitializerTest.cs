using DubUrl.OleDb.Mapping;
using DubUrl.OleDb.Querying.Reading;
using DubUrl.Querying.Reading.ResourceMatching;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Testing.Querying.Reading
{
    public class OleDbProviderResourceMatcherFactoryInitializerTest
    {
        [Test]
        public void Initialize_OleDbConnectivity_Value()
        {
            var factory = new ResourceMatcherFactory();
            factory.Initialize(new OleDbProviderResourceMatcherFactoryInitializer());
            Assert.That(factory.Instantiate(new OleDbConnectivity(), new[] { "mssql" } ), Is.TypeOf<OleDbProviderResourceMatcher>());
        }
    }
}
