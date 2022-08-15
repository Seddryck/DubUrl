using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    internal class LiteralMatch : BaseRegex
    {
        private string Text { get; }
        public LiteralMatch(string text)
            => Text = text;

        protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
            => stringBuilder.AppendEscaped(Text);
    }
}
