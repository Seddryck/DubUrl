using DubUrl.Mapping;
using DubUrl.Querying.Dialects.Functions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

public abstract class SqlFunctionMapperAttribute : Attribute
{
    public virtual Type SqlFunctionMapperType { get; protected set; } = typeof(AnsiFunctionMapper);

    public SqlFunctionMapperAttribute(Type dbTypeMapperType)
    {
        SqlFunctionMapperType = dbTypeMapperType;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SqlFunctionMapperAttribute<M> : SqlFunctionMapperAttribute where M : ISqlFunctionMapper
{
    public SqlFunctionMapperAttribute()
        : base(typeof(M))
    { }
}
