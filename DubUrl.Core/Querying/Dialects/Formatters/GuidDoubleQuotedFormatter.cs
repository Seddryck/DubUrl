using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;
public class GuidDoubleQuotedFormatter : GuidFormatter
{
    public override string Format(Guid value)
        => $"\"{value}\"";
}
