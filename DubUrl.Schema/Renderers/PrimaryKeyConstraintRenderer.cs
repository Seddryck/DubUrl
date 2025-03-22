using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Renderers;
public class PrimaryKeyConstraintRenderer
{
    public string? Name { get; }
    public Column[] Columns { get; }

    public PrimaryKeyConstraintRenderer(PrimaryKeyConstraint pk)
    {
        Name = pk.Name;
        Columns = pk.Columns.Values.ToArray();
    }
}
