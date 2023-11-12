using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading
{
    internal class DirectCommandMatchingOption : IResourceMatchingOption
    {
        public string[] Dialects { get; set; }
        public string? Connectivity { get; set; }

        public DirectCommandMatchingOption(string[] dialects, string? connectivity)
            => (Dialects, Connectivity) = (dialects,connectivity);
    }
}
