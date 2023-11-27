using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[Language(".sql")]
public class SqlLanguage : ILanguage
{
    public virtual string Extension { get; }

    public SqlLanguage()
        => Extension = GetType().GetCustomAttribute<LanguageAttribute>()?.Extension
            ?? throw new InvalidOperationException();
}
