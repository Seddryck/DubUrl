using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public interface IParametrizedCommand
{
    ImmutableArray<DubUrlParameter> Parameters { get; }
}
