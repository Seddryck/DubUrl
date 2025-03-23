using DubUrl.Mapping;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Renderers;

public abstract class DbTypeMapperAttribute : Attribute
{
    public virtual Type DbTypeMapperType { get; protected set; } = typeof(AnsiTypeMapper);

    public DbTypeMapperAttribute(Type dbTypeMapperType)
    {
        DbTypeMapperType = dbTypeMapperType;
    }
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class DbTypeMapperAttribute<M> : DbTypeMapperAttribute where M : IDbTypeMapper
{
    public DbTypeMapperAttribute()
        : base(typeof(M))
    { }
}
