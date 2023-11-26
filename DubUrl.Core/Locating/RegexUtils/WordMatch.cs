using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.RegexUtils;

public class WordMatch : LiteralMatch
{
    public WordMatch(string text)
        : base(text) { }

    protected internal override StringBuilder ToRegex(StringBuilder stringBuilder)
        => base.ToRegex(stringBuilder.Append(@"\b"));
}
