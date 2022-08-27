using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    public class CompositeRegex
    {
        protected BaseRegex[] Regexes { get; }

        public CompositeRegex(BaseRegex[] regexes)
            => Regexes = regexes;

        protected internal StringBuilder ToRegex(StringBuilder stringBuilder)
        {
            stringBuilder.Append('^');
            foreach (var regex in Regexes)
                regex.ToRegex(stringBuilder);
            stringBuilder.Append('$');
            return stringBuilder;
        }

        public string ToRegex()
            => ToRegex(new StringBuilder()).ToString();

        public override string ToString()
            => ToRegex();
    }
}
