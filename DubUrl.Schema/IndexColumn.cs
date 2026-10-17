using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class IndexColumn
{
    public string Name { get; }
    public IndexSortDirection? SortDirection { get; }

    public IndexColumn(string name)
        : this(name, null)
    { }

    public IndexColumn(string name, IndexSortDirection? sortDirection)
    {
        Name = name;
        SortDirection = sortDirection;
    }
}
