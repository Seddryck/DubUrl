using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing
{
    internal interface IParametrizedQuery
    {
        ImmutableArray<DubUrlParameter> Parameters { get; }
    }
}
