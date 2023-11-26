using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public class PositionalNamedParametrizer : PositionalParametrizer
{
    public PositionalNamedParametrizer()
    { }

    public override IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter)
    {
        var position = command.Parameters.Count + 1;
        var param = base.CreateParameter(command, parameter);
        param.ParameterName = position.ToString();
        return param;
    }

}
