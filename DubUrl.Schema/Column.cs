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
    public string? Description { get; }
    public NativeDatabaseType? NativeType { get; }
    public NativeTypeFallback NativeTypeFallback { get; }

    public Column(string name, System.Data.DbType type, object? defaultValue = null, IConstraint[]? constraints = null,
        string? description = null, NativeDatabaseType? nativeType = null, NativeTypeFallback nativeTypeFallback = NativeTypeFallback.Error)
    {
        (Name, Type, DefaultValue, Constraints, Description, NativeType, NativeTypeFallback) =
            (name, type, defaultValue, new ConstraintCollection(constraints ?? []), NormalizeDescription(description), nativeType, nativeTypeFallback);
    }

    private static string? NormalizeDescription(string? description)
        => string.IsNullOrWhiteSpace(description) ? null : description;
}
