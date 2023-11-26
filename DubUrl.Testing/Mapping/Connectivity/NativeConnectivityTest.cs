using DubUrl.Mapping;
using DubUrl.Mapping.Connectivity;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Mapping.Connectivity;

public class NativeConnectivityTest
{
    [Test]
    public void Alias_Get_Empty()
        => Assert.That(new NativeConnectivity().Alias, Is.EqualTo(string.Empty));
}
