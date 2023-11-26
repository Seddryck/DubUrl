using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.MicroOrm;

public class ReflectionCache : IReflectionCache
{
    private record CacheContent(PropertyInfo[] Properties, FieldInfo[] Fields) { };
    private readonly Dictionary<Type, CacheContent> dico = new();

    public void Add<T>(PropertyInfo[] properties, FieldInfo[] fields)
        => dico.Add(typeof(T), new CacheContent(properties, fields));
    public void Clear() => dico.Clear();
    public bool Exists<T>() => dico.ContainsKey(typeof(T));
    public (PropertyInfo[] properties, FieldInfo[] fields) Get<T>()
    {
        dico[typeof(T)].Deconstruct(out var properties, out var fields);
        return (properties, fields);
    }
    public void Remove<T>()
        => dico.Remove(typeof(T));
}
