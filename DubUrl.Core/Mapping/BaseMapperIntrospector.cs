using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public abstract class BaseMapperIntrospector : BaseIntrospector
{
    protected BaseMapperIntrospector(ITypesProbe probe)
        : base(probe) { }

    public abstract MapperInfo[] Locate();
}
