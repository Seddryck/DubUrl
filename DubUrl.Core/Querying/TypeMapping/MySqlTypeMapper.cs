using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class MySqlTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<MySqlTypeMapper> _instance = new(() => new MySqlTypeMapper());

    public static new MySqlTypeMapper Instance => _instance.Value;

    protected MySqlTypeMapper()
    {
        AddOrReplace(DbType.Int32, "INT");
        AddOrReplace(DbType.String, "TEXT");
        AddOrReplace(DbType.DateTimeOffset, "DATETIME");
        AddOrReplace(DbType.AnsiString, "VARCHAR");
    }
}
