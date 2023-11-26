using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

[AttributeUsage(AttributeTargets.Class)]
public class TypeMappingAttribute : Attribute
{
    public DbType DbType { get; }
    public TypeMappingAttribute(DbType dbType)
        => DbType = dbType;
}
