using DubUrl.Rewriting;
using DubUrl.Parsing;
using DubUrl.Rewriting.Tokening;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Rewriting;

internal class CrateDbRewriter : PostgresqlRewriter
{
    public CrateDbRewriter(DbConnectionStringBuilder csb)
        : base(csb)
    { }
}
