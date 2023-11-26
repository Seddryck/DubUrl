using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Casters;

internal class BooleanConverter : Converter<bool>
{
    public override bool Cast(object value)
        => Convert.ToBoolean(value);
}
