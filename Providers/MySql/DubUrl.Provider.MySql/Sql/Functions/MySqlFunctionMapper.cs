using DubUrl.Querying;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Functions;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Provider.MySql.Sql.Functions;
public class MySqlFunctionMapper : AnsiFunctionMapper
{
    private static readonly Lazy<MySqlFunctionMapper> _instance = new(() => new MySqlFunctionMapper());

    public static new MySqlFunctionMapper Instance => _instance.Value;

    protected MySqlFunctionMapper()
    { }
}
