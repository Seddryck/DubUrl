using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public class TSqlFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<TSqlFunctionMapper> _instance = new(() => new TSqlFunctionMapper());

    public static new TSqlFunctionMapper Instance => _instance.Value;

    protected TSqlFunctionMapper()
    {
        AddOrReplace(SqlFunction.Length, "LEN");
    }
}
