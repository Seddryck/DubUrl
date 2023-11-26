using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;

public class BooleanFormatter : IValueFormatter<bool>
{
    public virtual string Format(bool value)
        => value ? "TRUE" : "FALSE";
    public string Format(object obj)
        => obj is bool boolean ? Format(boolean) : throw new Exception();
}
