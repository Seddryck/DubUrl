using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Renderers;

namespace DubUrl.Querying.Templating;

internal interface ITemplatingProxy
{
    IRenderingProxy Prepare(string source);
    IRenderingProxy Prepare(string source, IDictionary<string, string> subTemplates, IDictionary<string, IDictionary<string, object?>> @dictionaries, IRenderer? renderer);
}

internal interface IRenderingProxy
{
    string Render(IDictionary<string, object?> parameters);
}
