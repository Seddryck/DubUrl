using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Rewriting.Implementation
{
    internal class TimescaleRewriter : PostgresqlRewriter
    {
        public TimescaleRewriter(DbConnectionStringBuilder csb)
            : base(csb)
        { }
    }
}
