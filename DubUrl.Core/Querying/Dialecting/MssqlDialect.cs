using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    public class MssqlDialect : BaseDialect
    {
        public MssqlDialect(string[] aliases)
            : base(aliases) { }
    }
}
