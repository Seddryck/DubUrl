using DubUrl.Querying.Dialects;
using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying
{
    public interface ICommandProvider
    {
        string Read(IDialect dialect, string? connectivity);
        bool Exists(IDialect dialect, string? connectivity, bool includeDefault = false);
    }
}
