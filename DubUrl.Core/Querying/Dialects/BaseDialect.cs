using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects;

public abstract class BaseDialect : IDialect
{
    public IRenderer Renderer { get; }
    public ICaster[] Casters { get; }
    public IDbTypeMapper DbTypeMapper { get; }
    public virtual string[] Aliases { get; }
    public virtual ILanguage Language { get; }

    public BaseDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters, IDbTypeMapper dbTypeMapper)
        => (Language, Aliases, Renderer, Casters, DbTypeMapper) = (language, aliases, renderer, casters, dbTypeMapper);
}
