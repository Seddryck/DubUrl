using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;
public class AssemblyTypesProbe : ITypesProbe
{
    public Assembly[] Assemblies { get; } = [typeof(SchemeMapperBuilder).Assembly];

    public AssemblyTypesProbe()
    { }

    public AssemblyTypesProbe(Assembly[] assemblies)
        => Assemblies = assemblies;

    public virtual IEnumerable<Type> Locate()
        => Assemblies.Aggregate(
                Array.Empty<Type>(), (types, asm)
                    => [.. types, .. asm.GetExportedTypes().Where(x => x.IsClass && !x.IsAbstract)]
            );
}
