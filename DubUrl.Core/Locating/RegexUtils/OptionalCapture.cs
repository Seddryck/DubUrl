using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    internal class OptionalCapture : BaseRegex
    {
        private string Option { get; }
        public OptionalCapture(string option)
            => Option = option;

        protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
            => stringBuilder.Append('(').AppendEscaped(Option).Append(@")?");
    }
}
