using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters
{
    public class SingleQuotedIdentifierFormatter : QuotedIdentifierFormatter
    {
        protected override string SurroundByQuotes(string value)
            => $"'{value}'";
    }
}
