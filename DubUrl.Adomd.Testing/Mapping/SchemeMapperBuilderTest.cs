using DubUrl.Adomd.Mapping;
using DubUrl.Mapping;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Testing.Mapping
{
    public class SchemeMapperBuilderTest
    {
        [SetUp]
        public void DefaultRegistration()
        {
            DbProviderFactories.RegisterFactory("Microsoft.AnalysisServices.AdomdClient", Wrappers.AdomdFactory.Instance);
        }

        [Test]
        [TestCase("powerbi", typeof(PowerBiPremiumMapper))]
        [TestCase("pbi", typeof(PowerBiPremiumMapper))]
        [TestCase("pbiazure", typeof(PowerBiPremiumMapper))]
        [TestCase("powerbipremium", typeof(PowerBiPremiumMapper))]
        [TestCase("pbipremium", typeof(PowerBiPremiumMapper))]
        [TestCase("powerbidesktop", typeof(PowerBiDesktopMapper))]
        [TestCase("pbix", typeof(PowerBiDesktopMapper))]
        [TestCase("pbidesktop", typeof(PowerBiDesktopMapper))]
        public void Instantiate_Scheme_CorrectType(string schemeList, Type expected)
        {
            var builder = new SchemeMapperBuilder(new[] { typeof(PowerBiPremiumMapper).Assembly });
            builder.Build();
            var result = builder.GetMapper(schemeList.Split(new[] { '+', ':' }));

            Assert.That(result, Is.Not.Null);
            Assert.That(result, Is.TypeOf(expected));
        }
    }
}
