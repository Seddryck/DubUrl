using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Registering
{
    public interface IProviderFactoriesDiscoverer
    {
        IEnumerable<Type> Execute();
    }
}
