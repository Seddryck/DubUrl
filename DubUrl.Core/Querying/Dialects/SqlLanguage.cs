using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Language(".sql", "Structured Query Language")]
public class SqlLanguage : ILanguage
{
    public virtual string Extension { get; }
    public virtual string FullName { get; }

    public SqlLanguage()
    {
        var attribute = GetType().GetCustomAttribute<LanguageAttribute>() ?? throw new InvalidOperationException();
        (Extension, FullName) = (attribute.Extension, attribute.FullName);
    }
}
