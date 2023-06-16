using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters
{
    internal class TrinoTimeSpanParser : Parser<TimeSpan>
    {
        protected override bool TryParse(string value, out TimeSpan result)
            => base.TryParse(value.Replace(" ", "."), out result);
    }
}
