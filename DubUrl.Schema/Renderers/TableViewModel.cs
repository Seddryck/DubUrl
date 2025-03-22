using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Renderers;

public class TableViewModel
{
    public string Name { get; }
    public Column[] Columns { get; }
    public PrimaryKeyConstraintViewModel? PrimaryKey { get; }

    public TableViewModel(Table table)
    {
        Name = table.Name;
        Columns = table.Columns.Values.ToArray();
        var pk = table.Constraints.OfType<PrimaryKeyConstraint>().SingleOrDefault();
        PrimaryKey = pk is null ? null : new PrimaryKeyConstraintViewModel(pk);
    }
}
