using DubUrl.Mapping.Database;
using DubUrl.Parsing;
using DubUrl.Querying.Dialecting;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation
{
    internal class MariaDbDataRewriter : MySqlDataRewriter
    {
        public MariaDbDataRewriter(DbConnectionStringBuilder csb)
            : base(csb)
        { }
    }
}
