using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Locating.OleDbProvider
{
    public interface IProviderLocator
    {
        string Locate();
    }
}
