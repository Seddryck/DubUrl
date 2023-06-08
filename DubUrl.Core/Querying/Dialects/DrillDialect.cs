using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    internal class DrillDialect : BaseDialect
    {
        public DrillDialect(string[] aliases)
            : base(aliases) { }
    }
}
