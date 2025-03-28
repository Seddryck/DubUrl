using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class Column
{
    public string Name { get; }
    public System.Data.DbType Type { get; }
    public object? DefaultValue { get; }
    public ConstraintCollection Constraints { get; }

    public Column(string name, System.Data.DbType type, object? defaultValue = null, IConstraint[]? constraints = null)
    {
        (Name, Type, DefaultValue, Constraints) = (name, type, defaultValue, new ConstraintCollection(constraints ?? []));
    }

    
}
