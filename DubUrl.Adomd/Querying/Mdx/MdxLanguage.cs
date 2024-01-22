using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Adomd.Querying.Mdx;

[Language(".mdx", "Multidimensional Expressions")]
public class MdxLanguage : ILanguage
{
    public virtual string Extension { get; }
    public virtual string FullName { get; }

    public MdxLanguage()
    {
        var attribute = GetType().GetCustomAttribute<LanguageAttribute>() ?? throw new InvalidOperationException();
        (Extension, FullName) = (attribute.Extension, attribute.FullName);
    }
}
