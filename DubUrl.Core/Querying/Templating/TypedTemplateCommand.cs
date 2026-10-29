using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Querying.Templating;

// Execution adapter only: all SQL rendering and binding stays in the existing engine.
internal sealed class TypedTemplateCommand(string source, IDictionary<string, object?> values) : ICommandProvider
{
    internal RenderedTemplateCommand RenderCommand(IDialect dialect, IParametrizer parametrizer)
        => InlineTemplateCommand.RenderBound(source, values, dialect.Renderer, parametrizer);

    public string Read(IDialect dialect, IConnectivity connectivity)
        => new InlineTemplateCommandFactory(dialect).Create(source).Render(values);

    public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;
}
