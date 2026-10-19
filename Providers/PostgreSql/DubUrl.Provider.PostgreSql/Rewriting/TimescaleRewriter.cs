using DubUrl.Rewriting;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Rewriting;

internal class TimescaleRewriter : PostgresqlRewriter
{
    public TimescaleRewriter(DbConnectionStringBuilder csb)
        : base(csb)
    { }
}
