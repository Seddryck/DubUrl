using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating;
internal class DidotRenderer
{
    private Didot.Core.IRenderer Renderer { get; }

    public DidotRenderer(Didot.Core.IRenderer renderer)
        => Renderer = renderer;

    public string Render(IDictionary<string, object?> parameters)
        => Renderer.Render(parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? DBNull.Value));
}

