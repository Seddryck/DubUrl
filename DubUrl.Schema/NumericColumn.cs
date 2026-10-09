using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class NumericColumn : Column
{
    public int Precision { get; }
    public int Scale { get; }

    public NumericColumn(string name, System.Data.DbType type, int precision, int scale, object? defaultValue = null, IConstraint[]? constraints = null,
        string? description = null, NativeDatabaseType? nativeType = null, NativeTypeFallback nativeTypeFallback = NativeTypeFallback.Error)
        : base(name, type, defaultValue, constraints, description, nativeType, nativeTypeFallback)
        => (Precision, Scale) = (precision, scale);
}
