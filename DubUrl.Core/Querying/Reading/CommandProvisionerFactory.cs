using DubUrl.Querying.Parametrizing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading;

public class CommandProvisionerFactory
{
    public virtual ICommandProvisioner[] Instantiate(ICommandProvider provider, ConnectionUrl connectionUrl)
    {
        if (provider is Templating.RenderedTemplateCommand rendered)
            return [new Templating.TemplateCommandProvisioner(() => rendered)];
        if (provider is Templating.TypedTemplateCommand typed)
            return [new Templating.TemplateCommandProvisioner(() => typed.RenderCommand(connectionUrl.Dialect, connectionUrl.Parametrizer))];
        if (provider is Templating.EmbeddedTemplateCommand template)
            return [new Templating.TemplateCommandProvisioner(() => template.RenderCommand(
                connectionUrl.Dialect, connectionUrl.Connectivity, connectionUrl.Parametrizer))];
        var list = new List<ICommandProvisioner>
        {
            new TextCommandProvisioner(provider, connectionUrl.Dialect, connectionUrl.Connectivity)
        };
        if (provider is IParametrizedCommand parametrizedProvider)
            list.Add(new ParametersCommandProvisioner(parametrizedProvider, connectionUrl.Parametrizer));
        return [.. list];
    }
}
