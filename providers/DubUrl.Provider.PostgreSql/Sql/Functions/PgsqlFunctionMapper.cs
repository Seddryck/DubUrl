using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.PostgreSql.Sql.Functions;
public class PgsqlFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<PgsqlFunctionMapper> _instance = new(() => new PgsqlFunctionMapper());

    public static new PgsqlFunctionMapper Instance => _instance.Value;

    protected PgsqlFunctionMapper()
    { }
}
