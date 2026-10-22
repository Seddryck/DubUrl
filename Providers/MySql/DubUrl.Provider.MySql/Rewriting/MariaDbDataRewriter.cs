using DubUrl.Provider.MySql.Mapping;
using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Rewriting;

internal class MariaDbDataRewriter : MySqlDataRewriter
{
    public MariaDbDataRewriter(DbConnectionStringBuilder csb)
        : base(csb)
    { }
}
