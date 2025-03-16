using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Table
{
    public string Name { get; set; }
    public ImmutableDictionary<string, Column> Columns { get; set; }

    public Table(string name, Column[] columns)
    {
        Name = name;
        Columns = columns.ToImmutableDictionary(c => c.Name);
    }
}
