using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.MicroOrm;

public class NoneReflectionCache : IReflectionCache
{
    public void Add<T>(PropertyInfo[] properties, FieldInfo[] fields) { }
    public void Clear() { }
    public bool Exists<T>() => false;
    public (PropertyInfo[] properties, FieldInfo[] fields) Get<T>() => throw new NotImplementedException();
    public void Remove<T>() { }
}
