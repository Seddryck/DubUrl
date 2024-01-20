using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

public abstract class BaseIntrospector
{
    protected record class AttributeInfo<T>(Type Type, T Attribute) { }
    private ITypesProbe Probe { get; }

    protected Type[]? _types;
    protected Type[] Types { get => _types ??= Probe.Locate().ToArray(); }

    protected BaseIntrospector(ITypesProbe probe)
        => (Probe) = (probe); 

    protected IEnumerable<AttributeInfo<T>> LocateAttribute<T>() where T : Attribute
    {
        var types = Types.Where(
                    x => x.GetCustomAttributes(typeof(T), false).Length > 0
                )
                .Select(x => (Type: x, Attribute: x.GetCustomAttribute<T>() ?? throw new InvalidOperationException()))
                .Select(x => new AttributeInfo<T>
                (
                    x.Type,
                    x.Attribute
                ));
        return types;
    }
}

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
//#if NET7_0_OR_GREATER
                    => types.Concat(asm.GetExportedTypes().Where(x => x.IsClass && !x.IsAbstract)).ToArray()
//#else
//                    => types.Concat(asm.GetTypes().Where(x => x.IsClass && !x.IsAbstract)).ToArray()
//#endif

            );
}

public interface ITypesProbe
{
    IEnumerable<Type> Locate();
}

public abstract class BaseMapperIntrospector : BaseIntrospector
{
    protected BaseMapperIntrospector(ITypesProbe probe)
        : base(probe) { }

    public abstract MapperInfo[] Locate();
}
