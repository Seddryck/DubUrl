using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.Trino.Sql.Casting;

internal class TrinoTimeSpanParser : Parser<TimeSpan>
{
    protected override bool TryParse(string value, out TimeSpan result)
        => base.TryParse(value.Replace(" ", "."), out result);
}
