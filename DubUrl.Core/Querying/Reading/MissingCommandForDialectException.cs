using DubUrl.Querying.Dialects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public class MissingCommandForDialectException : DubUrlException
    {
        public MissingCommandForDialectException(ICommandProvider provider, IDialect dialect)
            : base($"The '{provider}' matching with the dialect '{dialect.Aliases[0]}' wasn't found.") { }
    }
}