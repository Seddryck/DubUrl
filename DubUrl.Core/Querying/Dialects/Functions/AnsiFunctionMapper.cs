using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public class AnsiFunctionMapper : BaseFunctionMapper
{
    private static readonly Lazy<AnsiFunctionMapper> _instance = new(() => new AnsiFunctionMapper());

    public static AnsiFunctionMapper Instance => _instance.Value;

    protected AnsiFunctionMapper()
    {
        AddOrReplace(SqlFunction.Length, "LENGTH");
    }
}
