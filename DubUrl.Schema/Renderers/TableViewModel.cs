using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;

public class TableViewModel
{
    public string Name { get; }
    public DatabaseObjectName Identity { get; }
    public ColumnViewModel[] Columns { get; }
    public PrimaryKeyConstraintViewModel? PrimaryKey { get; }
    public NullableConstraint? Nullable { get; }
    public NotNullableConstraint? NotNullable { get; }
    public UniquenessConstraint? Unique { get; }
    public UniquenessConstraint[] UniqueConstraints { get; }
    public MembershipCheckConstraint[] MembershipChecks { get; }
    public RegexCheckConstraint[] RegexChecks { get; }
    public CheckConstraint[] Checks { get; }
    public ForeignKeyConstraintViewModel[] ForeignKeys { get; }
    public bool RenderForeignKeysInline { get; }
    public string? Description { get; }

    public TableViewModel(Table table, bool renderForeignKeysInline = false)
    {
        Name = table.Name;
        Identity = table.Identity;
        var primaryKey = table.Constraints.Get<PrimaryKeyConstraint>();
        PrimaryKey = primaryKey is null ? null : new PrimaryKeyConstraintViewModel(primaryKey);
        var primaryNames = primaryKey?.Columns.Keys.ToHashSet(StringComparer.Ordinal)
            ?? table.Columns.Values.Where(column => column.Constraints.Get<PrimaryKeyConstraint>() is not null)
                .Select(column => column.Name).ToHashSet(StringComparer.Ordinal);
        UniqueConstraints = table.Constraints.OfType<UniquenessConstraint>()
            .Where(unique => !primaryNames.SetEquals(unique.Columns.Select(column => column.Name))).ToArray();
        Columns = table.Columns.Values.Select(c => new ColumnViewModel(c, primaryKey is null, table.Identity,
            primaryNames.SetEquals([c.Name]))).ToArray();
        foreach (var column in Columns)
            column.IsKey |= primaryNames.Contains(column.Name)
                || UniqueConstraints.Any(unique => unique.Columns.Any(item => item.Name == column.Name));
        Nullable = table.Constraints.Get<NullableConstraint>();
        NotNullable = table.Constraints.Get<NotNullableConstraint>();
        Unique = table.Constraints.Get<UniquenessConstraint>();
        MembershipChecks = [.. table.Constraints.OfType<MembershipCheckConstraint>()];
        RegexChecks = [.. table.Constraints.OfType<RegexCheckConstraint>()];
        Checks = [.. table.Constraints.OfType<CheckConstraint>()];
        ForeignKeys = table.Constraints.OfType<ForeignKeyConstraint>()
            .Select(constraint => new ForeignKeyConstraintViewModel(constraint))
            .ToArray();
        RenderForeignKeysInline = renderForeignKeysInline;
        Description = table.Description;
    }
}
