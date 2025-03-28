using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public interface ISqlFunctionMapper
{
    IDictionary<string, object> ToDictionary();
}
