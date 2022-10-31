using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceMatching
{
    public interface IResourceMatcher
    {
        string? Execute(string id, string[] resourceNames);
    }
}
