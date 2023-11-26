using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Reading;

public interface ICommandProvisioner
{
    void Execute(IDbCommand command);
}
