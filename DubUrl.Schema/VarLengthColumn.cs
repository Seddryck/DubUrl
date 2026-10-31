using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class VarLengthColumn : Column
{
    /// <summary>Intentional unbounded string; rejected by dialects without a safe representation.</summary>
    public const int Unbounded = -1;
    public int Length { get; }

    public VarLengthColumn(string name, System.Data.DbType type, int size, object? defaultValue = null, IConstraint[]? constraints = null,
        string? description = null, NativeDatabaseType? nativeType = null, NativeTypeFallback nativeTypeFallback = NativeTypeFallback.Error,
        ColumnGeneration? generation = null)
        : base(name, type, defaultValue, constraints, description, nativeType, nativeTypeFallback, generation)
    {
        if (size != Unbounded && size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Length must be positive or VarLengthColumn.Unbounded.");
        Length = size;
    }
}
