using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    internal abstract class BaseRegex
    {
        protected internal abstract StringBuilder ToRegex(StringBuilder stringBuilder);
        public string ToRegex()
            => ToRegex(new StringBuilder()).ToString();
    }
}
