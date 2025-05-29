using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Renderers;

public class IndexViewModel
{
    public string Name { get; }
    public string TableName { get; }
    public IndexColumnViewModel[] Columns { get; }

    public IndexViewModel(Index index)
    {
        Name = index.Name;
        TableName = index.TableName;
        Columns = index.Columns.Values.Select(c => new IndexColumnViewModel(c)).ToArray();
    }
}
