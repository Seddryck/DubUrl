using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.Dialects.Functions;
public class FirebirdSqlFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<FirebirdSqlFunctionMapper> _instance = new(() => new FirebirdSqlFunctionMapper());

    public static new FirebirdSqlFunctionMapper Instance => _instance.Value;

    protected FirebirdSqlFunctionMapper()
    { }
}
