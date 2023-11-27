using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Renderers;
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
    public virtual string[] Aliases { get; }
    public virtual ILanguage Language { get; }

    public BaseDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
        => (Language, Aliases, Renderer, Casters) = (new SqlLanguage(), aliases, renderer, casters);

    public BaseDialect(ILanguage language, string[] aliases, IRenderer renderer, ICaster[] casters)
        => (Language, Aliases, Renderer, Casters) = (language, aliases, renderer, casters);
}
