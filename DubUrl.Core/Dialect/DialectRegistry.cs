//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using DubUrl.Querying.Dialects;

//namespace DubUrl.Dialect;
//public class DialectRegistry
//{
//    private static DialectRegistry? Instance { get; }
//    private static readonly object _lock = new object();
//    private readonly IReadOnlyDictionary<Type, IDialect> Dialects { get; }

//    internal DialectRegistry(IDictionary<Type, IDialect> dialects)
//        => Dialects = new Dictionary<Type, IDialect>(dialects);

//    public static DialectRegistry Instance
//    {
//        get
//        {
//            lock (_lock)
//            {
//                return Instance ??= new DialectRegistryBuilder();
//            }
//        }
//    }

//    public IDialect GetDialect(Type name)
//    {
//        if (_dialects.TryGetValue(name, out var dialect))
//            return dialect;
//        throw new ArgumentException($"Dialect {name} not found");
//    }
//}
