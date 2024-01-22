using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;
internal class LanguageComparer : IEqualityComparer<ILanguage>
{
    public bool Equals(ILanguage? x, ILanguage? y) 
        => (x?.Extension.Equals(y?.Extension) ?? false)
            && (x?.FullName.Equals(y?.FullName) ?? false);
    public int GetHashCode([DisallowNull] ILanguage obj) 
        => obj.Extension.GetHashCode();
}
