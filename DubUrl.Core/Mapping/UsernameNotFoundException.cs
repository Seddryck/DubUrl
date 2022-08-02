using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping
{
    public class UsernameNotFoundException : DubUrlException
    {
        public UsernameNotFoundException()
            : base($"The url must contain a username.")
        { }
    }
}
