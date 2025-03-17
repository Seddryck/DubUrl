using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
public class Table
{
    public string Name { get; }
    public ImmutableDictionary<string, Column> Columns => ColumnValues.ToImmutableDictionary(c => c.Name);
    public Column[] ColumnValues { get; }

    public Table(string name, Column[] columns)
    {
        Name = name;
        ColumnValues = columns;
    }
}
