using DubUrl.Querying.Dialecting;
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
            : base($"The '{provider.ToString()}' matching with the dialect '{dialect}' wasn't found.") { }
    }
}
