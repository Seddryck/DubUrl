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
        string Read(string[] dialects);
        bool Exists(string[] dialects, bool includeDefault = false);
    }
}
