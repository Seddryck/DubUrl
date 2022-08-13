using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialecting
{
    public abstract class BaseDialect : IDialect
    {
        public virtual string[] Aliases { get; }
        public BaseDialect(string[] aliases)
            => Aliases = aliases;
    }
}
