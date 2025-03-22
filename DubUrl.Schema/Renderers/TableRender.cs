using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Renderers;

public class TableRender
{
    public string Name { get; }
    public Column[] Columns { get; }
    public PrimaryKeyConstraintRenderer? PrimaryKey { get; set; }

    public TableRender(Table table)
    {
        Name = table.Name;
        Columns = table.Columns.Values.ToArray();
        var pk = table.Constraints.OfType<PrimaryKeyConstraint>().SingleOrDefault();
        PrimaryKey = pk is null ? null : new PrimaryKeyConstraintRenderer(pk);
    }
}
