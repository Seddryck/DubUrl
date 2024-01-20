using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Formatters;
public class GuidFormatter : IValueFormatter<Guid>
{
    public string Format(Guid value)
        => $"'{value}'";

    public string Format(object obj)
        => obj is Guid value
                ? Format(value)
                : obj is string str
                    ? Format(str)   
                    : throw new Exception();

    public virtual string Format(string value)
        => Guid.TryParse(value, out var guid) ? Format(guid) : throw new Exception();
}
