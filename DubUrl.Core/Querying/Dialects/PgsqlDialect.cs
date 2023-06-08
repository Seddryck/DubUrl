using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    internal class PgsqlDialect : BaseDialect
    {
        public PgsqlDialect(string[] aliases)
            : base(aliases) { }
    }
}
