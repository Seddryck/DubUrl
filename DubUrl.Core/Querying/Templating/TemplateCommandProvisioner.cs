using System.Data;
using DubUrl.Querying.Reading;

namespace DubUrl.Querying.Templating;

internal sealed class TemplateCommandProvisioner(Func<RenderedTemplateCommand> render) : ICommandProvisioner
{
    public void Execute(IDbCommand command) => render().Bind(command);
}
