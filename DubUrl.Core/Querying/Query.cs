using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    public interface IQuery
    {
        string Read(IDialect dialect);
        bool Exists(IDialect dialect, bool includeDefault = false);
    }
}
