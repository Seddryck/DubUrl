using DubUrl.Registering;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Testing.Registering;

public class ReferencedAssembliesDiscovererTest
{
    [Test]
    public void Execute_CurrentAssembly_DoesntLoadMoreAssemblies()
    {
        var countLoaded = AppDomain.CurrentDomain.GetAssemblies().Length;
        var discover = new ReferencedAssembliesDiscoverer(GetType().Assembly);
        discover.Execute();
        Assert.That(countLoaded, Is.EqualTo(AppDomain.CurrentDomain.GetAssemblies().Length));
    }
}
