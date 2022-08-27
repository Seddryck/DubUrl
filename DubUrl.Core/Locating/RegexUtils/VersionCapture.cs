using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils
{
    public class VersionCapture : BaseRegex
    {
        protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
            => stringBuilder.Append(@"([0-9]{1,2}(?:\.[0-9]{1,2}){0,2})");
    }

    public class VersionCapture<T> : VersionCapture
    {
        public VersionCapture() : base() { }
    }
}
