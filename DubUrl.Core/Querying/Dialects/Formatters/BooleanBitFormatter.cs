using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

internal class BooleanBitFormatter : IValueFormatter<bool>
{
    public virtual string Format(bool value)
        => value ? "1" : "0";
    public string Format(object obj)
        => obj is bool boolean ? Format(boolean) : throw new Exception();
}
