using DubUrl.Mapping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading.ResourceMatching
{
    public interface IResourceMatcherFactoryInitializer
    {
        Func<string[], IResourceMatcher> Execute();
        IConnectivity Connectivity { get; }
    }
}
