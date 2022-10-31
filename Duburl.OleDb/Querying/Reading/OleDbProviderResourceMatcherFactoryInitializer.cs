using DubUrl.Mapping;
using DubUrl.OleDb.Mapping;
using DubUrl.Querying.Reading.ResourceMatching;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.OleDb.Querying.Reading
{
    internal class OleDbProviderResourceMatcherFactoryInitializer : IResourceMatcherFactoryInitializer
    {
        public Func<string[], IResourceMatcher> Execute()
            => (string[] dialects) => new OleDbProviderResourceMatcher(dialects);

        public IConnectivity Connectivity
            => new OleDbConnectivity();
    }
}
