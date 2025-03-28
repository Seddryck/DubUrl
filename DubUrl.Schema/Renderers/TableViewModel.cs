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
    public ColumnViewModel[] Columns { get; }
    public PrimaryKeyConstraint? PrimaryKey { get; }
    public NullableConstraint? Nullable { get; }
    public NotNullableConstraint? NotNullable { get; }
    public UniquenessConstraint? Unique { get; }
    public CheckConstraint[] Checks { get; } = [];

    public TableViewModel(Table table)
    {
        Name = table.Name;
        Columns = table.Columns.Values.Select(c => new ColumnViewModel(c)).ToArray();
        PrimaryKey = table.Constraints.Get<PrimaryKeyConstraint>();
        Nullable = table.Constraints.Get<NullableConstraint>();
        NotNullable = table.Constraints.Get<NotNullableConstraint>();
        Unique = table.Constraints.Get<UniquenessConstraint>();
    }
}
