using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating;
internal class DidotRenderer
{
    private Didot.Core.IRenderer Renderer { get; }
    private readonly AsyncLocal<Exception?>? _failure;

    public DidotRenderer(Didot.Core.IRenderer renderer, AsyncLocal<Exception?>? failure = null)
        => (Renderer, _failure) = (renderer, failure);

    public string Render(IDictionary<string, object?> parameters)
    {
        if (_failure is not null) _failure.Value = null;
        try
        {
            var result = Renderer.Render(parameters.ToDictionary(kvp => kvp.Key, kvp => kvp.Value ?? DBNull.Value));
            if (_failure?.Value is Exception exception)
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
            return result;
        }
        finally
        {
            if (_failure is not null) _failure.Value = null;
        }
    }
}

