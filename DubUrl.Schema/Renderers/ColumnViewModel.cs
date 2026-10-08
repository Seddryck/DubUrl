using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;
public class ColumnViewModel
{
    public string Name { get; }
    public string Type { get; }
    public int Length { get; }
    public int Precision { get; }
    public int Scale { get; }
    public Constraint? Nullable { get; }
    public Constraint? NotNullable { get; }
    public Constraint? Unique { get; }
    public PrimaryKeyConstraintViewModel? PrimaryKey { get; }
    public CheckConstraint[] Checks { get; }
    public MembershipCheckConstraint[] MembershipChecks { get; }
    public RegexCheckConstraint[] RegexChecks { get; }

    public object? DefaultValue { get; }
    public bool HasDefaultValue { get; }
    public string? Description { get; }
    public NativeDatabaseType? NativeType { get; }
    public NativeTypeFallback NativeTypeFallback { get; }

    public ColumnViewModel(Column column)
    {
        Name = column.Name;
        Type = column.Type.ToString();
        DefaultValue = column.DefaultValue;
        HasDefaultValue = column.DefaultValue is not null;
        Description = column.Description;
        NativeType = column.NativeType;
        NativeTypeFallback = column.NativeTypeFallback;
        Nullable = column.Constraints.Get<NullableConstraint>();
        NotNullable = column.Constraints.Get<NotNullableConstraint>();
        Unique = column.Constraints.Get<UniquenessConstraint>();
        Checks = [.. column.Constraints.OfType<CheckConstraint>()];
        MembershipChecks = [.. column.Constraints.OfType<MembershipCheckConstraint>()];
        RegexChecks = [.. column.Constraints.OfType<RegexCheckConstraint>()];
        if (column is VarLengthColumn varLength)
            Length = varLength.Length;
        if (column is NumericColumn numeric)
        {
            Precision = numeric.Precision;
            Scale = numeric.Scale;
        }
    }
}
