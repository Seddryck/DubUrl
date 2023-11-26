using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Adomd.Mapping;

public class AdomdMapperIntrospector : NativeMapperIntrospector
{
    public AdomdMapperIntrospector()
        : this(new[] { typeof(AdomdMapperIntrospector).Assembly }) { }
    public AdomdMapperIntrospector(Assembly[] assemblies)
        : base(assemblies) { }
    public AdomdMapperIntrospector(ITypesProbe probe)
        : base(probe) { }
}
