using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public abstract class BaseFunctionMapper : ISqlFunctionMapper
{
    private Dictionary<SqlFunction, string> Mappings { get; } = [];

    protected void AddOrReplace(SqlFunction sqlFunction, string dialectFunction)
    {
        if (!Mappings.TryAdd(sqlFunction, dialectFunction))
            Mappings[sqlFunction] = dialectFunction;
    }

    public IDictionary<string, object> ToDictionary()
        => Mappings.ToDictionary(kvp => kvp.Key.ToString(), kvp => (object)kvp.Value);
}
