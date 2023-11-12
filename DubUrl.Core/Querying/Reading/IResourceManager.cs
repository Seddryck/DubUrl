using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    public interface IResourceManager
    {
        string ReadResource(string fullResourceName);
        ParameterInfo[] ReadParameters(string fullResourceName);
        bool Any(string id, IResourceMatchingOption option);
        string BestMatch(string id, IResourceMatchingOption option);
    }
}
