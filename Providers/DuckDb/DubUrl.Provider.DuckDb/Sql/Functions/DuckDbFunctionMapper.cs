using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.DuckDb.Sql.Functions;
public class DuckDbFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<DuckDbFunctionMapper> _instance = new(() => new DuckDbFunctionMapper());

    public static new DuckDbFunctionMapper Instance => _instance.Value;

    protected DuckDbFunctionMapper()
    { }
}
