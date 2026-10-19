using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Rewriting;

internal class MariaDbConnectorRewriter : MySqlConnectorRewriter
{
    public MariaDbConnectorRewriter(DbConnectionStringBuilder csb)
        : base(csb)
    { }
}
