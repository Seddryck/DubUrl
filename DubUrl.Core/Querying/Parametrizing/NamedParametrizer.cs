using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public class NamedParametrizer : PositionalParametrizer
{
    public NamedParametrizer()
        : base() { }

    public override IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter)
    {
        var param = base.CreateParameter(command, parameter);
        param.ParameterName = parameter.Name;
        return param;
    }
}
