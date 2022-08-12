using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    public interface IDialect
    {
        string[] Aliases { get; }
    }
}
