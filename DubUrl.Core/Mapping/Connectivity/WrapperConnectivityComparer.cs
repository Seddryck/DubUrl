using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Mapping.Connectivity;
internal class WrapperConnectivityComparer : IEqualityComparer<IWrapperConnectivity>
{
    public bool Equals(IWrapperConnectivity? x, IWrapperConnectivity? y)
    {
        if (x == null && y == null)
            return true;
        if (x == null || y == null)
            return false;
        return x.Alias == y.Alias && x.GetType() == y.GetType();
    }
        
    public int GetHashCode([DisallowNull] IWrapperConnectivity obj)
        => obj.Alias.GetHashCode();

}
