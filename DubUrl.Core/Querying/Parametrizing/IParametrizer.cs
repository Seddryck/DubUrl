using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Parametrizing;

public interface IParametrizer
{
    IDbDataParameter CreateParameter(IDbCommand command, DubUrlParameter parameter);
}
