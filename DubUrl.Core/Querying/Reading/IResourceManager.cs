using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading;

public interface IResourceManager
{
    string ReadResource(string fullResourceName);
    ParameterInfo[] ReadParameters(string fullResourceName);
    bool Any(string id, string[] dialects, string? connectivity);
    string BestMatch(string id, string[] dialects, string? connectivity);
}
