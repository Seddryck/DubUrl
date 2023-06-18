using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping.Connectivity
{
    public class OdbcConnectivityTest
    {
        [Test]
        public void Alias_Get_Odbc()
            => Assert.That(new OdbcConnectivity().Alias, Is.EqualTo("odbc"));
    }
}
