using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils;

internal class SpaceMatch : BaseRegex
{
    protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
        => stringBuilder.Append(@"\s");
}
