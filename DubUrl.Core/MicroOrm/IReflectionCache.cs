using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.MicroOrm;

public interface IReflectionCache
{
    bool Exists<T>();
    (PropertyInfo[] properties, FieldInfo[] fields) Get<T>();
    void Add<T>(PropertyInfo[] properties, FieldInfo[] fields);
    void Remove<T>();
    void Clear();
}
