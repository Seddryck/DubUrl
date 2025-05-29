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

    public IndexColumnViewModel(IndexColumn column)
    {
        Name = column.Name;
    }
}
