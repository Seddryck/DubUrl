using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Prql
{
    internal interface IPrqlCompiler
    {
        string ToSql(string text, IDialect dialect);
    }
}
