using DubUrl.Querying.Dialects;
using System;
using DubUrl.Locating;
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
        var types = Types.SelectMany(type => (Probe is AttributeTypesProbe snapshot
                    ? snapshot.Attributes(type).OfType<T>() : type.GetCustomAttributes<T>(false))
                .Select(attribute => (Type: type, Attribute: attribute)))
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
    public Assembly[] Assemblies { get; }

    public AssemblyTypesProbe()
        => Assemblies = GetDefaultAssemblies();

    private static Assembly[] GetDefaultAssemblies()
        => [typeof(SchemeRegistry).Assembly, .. ApplicationProviderDiscovery.GetAssemblies()];

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

// A module's discovery pass shares one attribute snapshot between native and wrapper introspectors.
internal sealed class AttributeTypesProbe : ITypesProbe
{
    private readonly Dictionary<Type, Attribute[]> attributes;
    public AttributeTypesProbe(IEnumerable<Type> types)
    {
        attributes = types.Distinct().ToDictionary(type => type, type => type.GetCustomAttributes(false).OfType<Attribute>().ToArray());
        // A wrapper assembly contains locators for many families. Only inspect the families in this module.
        foreach (var type in attributes.Keys.ToArray())
            if (attributes[type].OfType<LocatorAttribute>().Any(locator => !attributes.ContainsKey(locator.Database)))
                attributes.Remove(type);
    }
    public IEnumerable<Type> Locate() => attributes.Keys;
    public IEnumerable<Attribute> Attributes(Type type) => attributes[type];
}

public abstract class BaseMapperIntrospector : BaseIntrospector
{
    protected BaseMapperIntrospector(ITypesProbe probe)
        : base(probe) { }

    public abstract MapperInfo[] Locate();
}
