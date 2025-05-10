using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;

namespace DubUrl.Dialect;
internal class DialectIntrospector
{
    private readonly List<Assembly> _assemblies = [];

    public Type[] Locate()
    {
        var probe = new AssemblyTypesProbe([.. _assemblies]);
        return probe.Locate().Where(t => t.IsAssignableTo(typeof(IDialect))).ToArray();
    }

    public void Include(Assembly asm)
        => _assemblies.Add(asm);

    public void Include(params Assembly[] assemblies)
        => _assemblies.AddRange(assemblies);
}
