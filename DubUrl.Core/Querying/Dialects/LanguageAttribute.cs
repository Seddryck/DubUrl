using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class LanguageAttribute : Attribute
{
    public LanguageAttribute(string extension, string fullName)
        => (Extension, FullName) = (extension.StartsWith('.') ? extension : $".{extension}", fullName);

    public virtual string Extension { get; }
    public virtual string FullName { get; }
}
