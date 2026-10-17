using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;
public class IndexColumnViewModel
{
    public string Name { get; }
    public bool IsAscending { get; }
    public bool IsDescending { get; }

    public IndexColumnViewModel(IndexColumn column)
    {
        Name = column.Name;
        IsAscending = column.SortDirection == IndexSortDirection.Ascending;
        IsDescending = column.SortDirection == IndexSortDirection.Descending;
    }
}
