using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    internal class CockRoachDialect : BaseDialect
    {
        public CockRoachDialect(string[] aliases)
            : base(aliases) { }
    }
}
