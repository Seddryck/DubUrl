using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Column
{
    public string Name { get; }
    public DbType Type { get; }
    public bool IsNullable { get; } = false;
    public object? DefaultValue { get; }

    public Column(string name, DbType type, bool isNullable = false, object? defaultValue = null)
    {
        (Name, Type, IsNullable, DefaultValue) = (name, type, isNullable, defaultValue);
    }
}
