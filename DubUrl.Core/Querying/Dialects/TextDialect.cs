using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects
{
    internal class TextDialect : BaseDialect
    {
        public TextDialect(string[] aliases)
            : base(aliases) { }
    }
}
