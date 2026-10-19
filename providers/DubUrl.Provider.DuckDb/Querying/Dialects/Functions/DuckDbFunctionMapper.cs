using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public class DuckDbFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<DuckDbFunctionMapper> _instance = new(() => new DuckDbFunctionMapper());

    public static new DuckDbFunctionMapper Instance => _instance.Value;

    protected DuckDbFunctionMapper()
    { }
}
