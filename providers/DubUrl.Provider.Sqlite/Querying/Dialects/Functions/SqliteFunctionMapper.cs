using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public class SqliteFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<SqliteFunctionMapper> _instance = new(() => new SqliteFunctionMapper());

    public static new SqliteFunctionMapper Instance => _instance.Value;
    protected SqliteFunctionMapper()
    { }
}
