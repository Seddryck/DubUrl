using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AlternativeMapperAttribute<T, P> : AlternativeMapperAttribute 
    where T : IDatabase
    where P : IParametrizer
{
    public AlternativeMapperAttribute(string providerInvariantName)
        : base(
              typeof(T)
              , typeof(P)
              , providerInvariantName
        )
    { }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class AlternativeMapperAttribute : BaseMapperAttribute
{
    public AlternativeMapperAttribute(Type database, Type parametrizer, string providerInvariantName)
        : base(database, parametrizer, providerInvariantName)
    { }
}
