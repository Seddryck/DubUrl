using DubUrl.Querying.Reading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Templating
{
    public interface IResourceTemplateManager : IResourceManager
    {
        IDictionary<string, string> ListResources(string directory, string[] dialects, string? connectivity, string extension);
    }
}
