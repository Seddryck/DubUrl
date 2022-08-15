using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    internal class AnyOfCapture : BaseRegex
    {
        private string[] Options { get; }
        public AnyOfCapture(string[] options)
            => Options = options;

        protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
        {
            stringBuilder.Append('(');
            foreach (var option in Options)
            {
                stringBuilder.AppendEscaped(option);
                stringBuilder.Append('|');
            }
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            stringBuilder.Append(')');
            return stringBuilder;
        }
    }

    internal class AnyOfCapture<T> : AnyOfCapture
    {
        public AnyOfCapture(string[] options) : base(options) { }
    }
}
