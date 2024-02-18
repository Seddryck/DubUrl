using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;
public class SchemeHandlerIntrospector : BaseIntrospector
{
    public SchemeHandlerIntrospector(ITypesProbe probe)
        : base(probe)
    { }

    public Type[] Locate()
        => Types.Where(x => x.IsAssignableTo(typeof(ISchemeHandler))).ToArray();
}
