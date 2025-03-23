using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class NumericColumn : Column
{
    public int Precision { get; }
    public int Scale { get; }

    public NumericColumn(string name, DbType type, int precision, int scale, bool isNullable = false, object? defaultValue = null)
        : base(name, type, isNullable, defaultValue)
        => (Precision, Scale) = (precision, scale);
}
