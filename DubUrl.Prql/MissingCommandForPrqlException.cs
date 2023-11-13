using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying;
using DubUrl.Querying.Dialects;

namespace DubUrl.Prql
{
    internal class MissingCommandForPrqlException : DubUrlException
    {
        public MissingCommandForPrqlException(string basePath)
            : base($"Missing PRQL query '{basePath}'.") { }
    }
}
