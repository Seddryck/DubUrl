using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class LanguageAttribute : Attribute
{
    public LanguageAttribute(string extension)
        => Extension = extension.StartsWith('.') ? extension : $".{extension}";

    public virtual string Extension { get; }
}
