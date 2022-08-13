using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    internal class MySqlDialect : BaseDialect
    {
        public MySqlDialect(string[] aliases)
            : base(aliases) { }
    }
}
