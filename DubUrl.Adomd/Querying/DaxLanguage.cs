using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects;

namespace DubUrl.Adomd.Querying;

[Language(".dax")]
public class DaxLanguage : ILanguage
{
    public virtual string Extension { get; }

    public DaxLanguage()
    {
        Extension = GetType().GetCustomAttribute<LanguageAttribute>()?.Extension
            ?? throw new InvalidOperationException();
    }
}
