using System.Collections.Immutable;
using System.Data;
using DubUrl.Mapping;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;

namespace DubUrl.Querying.Templating;

/// <summary>SQL text and bindings generated together by one template evaluation.</summary>
public sealed class RenderedTemplateCommand : ICommandProvider
{
    public string CommandText { get; }
    public ImmutableArray<TemplateBinding> Parameters { get; }
    private readonly IParametrizer _parametrizer;

    internal RenderedTemplateCommand(string text, IEnumerable<TemplateBinding> parameters, IParametrizer parametrizer)
        => (CommandText, Parameters, _parametrizer) = (text, [.. parameters], parametrizer);

    public string Read(IDialect dialect, IConnectivity connectivity) => CommandText;
    public bool Exists(IDialect dialect, IConnectivity connectivity, bool includeDefault = false) => true;

    internal void Bind(IDbCommand command)
    {
        command.CommandType = CommandType.Text;
        command.CommandText = CommandText;
        _parametrizer.ConfigureTemplateCommand(command);
        foreach (var binding in Parameters)
            command.Parameters.Add(_parametrizer.CreateTemplateParameter(command, binding));
    }
}
