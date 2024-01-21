using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Adomd.Querying;

[Language(".dax", "Data Analysis Expressions")]
public class DaxLanguage : ILanguage
{
    public virtual string Extension { get; }
    public virtual string FullName { get; }

    public DaxLanguage()
    {
        var attribute = GetType().GetCustomAttribute<LanguageAttribute>() ?? throw new InvalidOperationException();
        (Extension, FullName) = (attribute.Extension, attribute.FullName);
    }
}
