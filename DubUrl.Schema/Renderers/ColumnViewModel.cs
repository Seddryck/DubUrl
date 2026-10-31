using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;

public class ColumnViewModel
{
    public DatabaseObjectName? TableIdentity { get; }
    public string Name { get; }
    public string Type { get; }
    public int Length { get; }
    public int? DeclaredLength { get; }
    public bool IsKey { get; internal set; }
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
    public ColumnGeneration? Generation { get; }

    public ColumnViewModel(Column column, bool includePrimaryKey = true)
        : this(column, includePrimaryKey, null)
    { }

    public ColumnViewModel(Column column, bool includePrimaryKey, DatabaseObjectName? tableIdentity)
        : this(column, includePrimaryKey, tableIdentity, false)
    { }

    public ColumnViewModel(Column column, bool includePrimaryKey, DatabaseObjectName? tableIdentity, bool suppressUnique)
    {
        TableIdentity = tableIdentity;
        Name = column.Name;
        Type = column.Type.ToString();
        DefaultValue = column.DefaultValue;
        HasDefaultValue = column.DefaultValue is not null;
        Description = column.Description;
        NativeType = column.NativeType;
        NativeTypeFallback = column.NativeTypeFallback;
        Generation = column.Generation;
        Nullable = column.Constraints.Get<NullableConstraint>();
        NotNullable = column.Constraints.Get<NotNullableConstraint>();
        var columnPrimaryKey = column.Constraints.Get<PrimaryKeyConstraint>();
        Unique = GetUniqueness(column, includePrimaryKey, suppressUnique, columnPrimaryKey);
        PrimaryKey = CreatePrimaryKey(includePrimaryKey, columnPrimaryKey);
        Checks = [.. column.Constraints.OfType<CheckConstraint>()];
        MembershipChecks = [.. column.Constraints.OfType<MembershipCheckConstraint>()];
        RegexChecks = [.. column.Constraints.OfType<RegexCheckConstraint>()];
        IsKey = columnPrimaryKey is not null || Unique is not null;
        if (column is VarLengthColumn varLength)
            DeclaredLength = Length = varLength.Length;
        if (column is NumericColumn numeric)
        {
            Precision = numeric.Precision;
            Scale = numeric.Scale;
        }
    }

    private static Constraint? GetUniqueness(Column column, bool includePrimaryKey, bool suppressUnique, PrimaryKeyConstraint? primaryKey)
        => suppressUnique || (includePrimaryKey && primaryKey is not null)
            ? null : column.Constraints.Get<UniquenessConstraint>();

    private static PrimaryKeyConstraintViewModel? CreatePrimaryKey(bool includePrimaryKey, PrimaryKeyConstraint? primaryKey)
        => includePrimaryKey && primaryKey is not null ? new PrimaryKeyConstraintViewModel(primaryKey) : null;

}
