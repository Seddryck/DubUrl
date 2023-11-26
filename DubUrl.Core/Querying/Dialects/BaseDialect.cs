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

    public BaseDialect(string[] aliases, IRenderer renderer)
        : this(aliases, renderer, Array.Empty<ICaster>()) {}

    public BaseDialect(string[] aliases, IRenderer renderer, ICaster[] casters)
        => (Aliases, Renderer, Casters) = (aliases, renderer, casters);
}
