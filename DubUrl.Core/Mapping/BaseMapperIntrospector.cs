using DubUrl.Querying.Dialecting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public abstract class BaseIntrospector
    {
        protected record struct AttributeInfo<T>(Type Type, T Attribute) { }
        private AssemblyClassesIntrospector ClassesIntrospector { get; } = new();

        protected Type[]? _types;
        protected Type[] Types { get => _types ??= ClassesIntrospector.Locate().ToArray(); }

        protected BaseIntrospector(AssemblyClassesIntrospector introspector)
            => (ClassesIntrospector) = (introspector);

        protected IEnumerable<AttributeInfo<T>> LocateAttribute<T>() where T : Attribute
            => Types.Where(
                        x => x.GetCustomAttributes(typeof(T), false).Length > 0
                    )
                    .Select(x => (Type: x, Attribute: x.GetCustomAttribute<T>() ?? throw new InvalidOperationException()))
                    .Select(x => new AttributeInfo<T>
                    (
                        x.Type,
                        x.Attribute
                    ));

        public class AssemblyClassesIntrospector
        {
            public virtual IEnumerable<Type> Locate()
                => typeof(SchemeMapperBuilder).Assembly.GetTypes().Where(x => x.IsClass);
        }
    }

    public abstract class BaseMapperIntrospector : BaseIntrospector
    {
        protected BaseMapperIntrospector(AssemblyClassesIntrospector introspector)
            : base(introspector) { }

        public abstract IEnumerable<MapperInfo> Locate();
    }
}
