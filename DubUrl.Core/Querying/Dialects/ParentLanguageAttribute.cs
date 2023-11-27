using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ParentLanguageAttribute : Attribute
{
    public virtual ILanguage Language { get; }

    public ParentLanguageAttribute(Type language)
        => Language = (ILanguage)Activator.CreateInstance(language)!;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ParentLanguageAttribute<T> : ParentLanguageAttribute where T : ILanguage
{
    public ParentLanguageAttribute()
        : base(typeof(T)) { }
}
