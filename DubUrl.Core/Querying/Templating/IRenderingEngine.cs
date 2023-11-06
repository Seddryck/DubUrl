using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Querying.Dialects.Renderers;

namespace DubUrl.Querying.Templating
{
    internal interface IRenderingEngine
    {
        string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, object?> parameters, IRenderer? renderer);
        string Render(string source, IDictionary<string, string> subTemplates, IDictionary<string, IDictionary<string, object?>> @dictionaries, IDictionary<string, object?> parameters, IRenderer? renderer);
    }
}
