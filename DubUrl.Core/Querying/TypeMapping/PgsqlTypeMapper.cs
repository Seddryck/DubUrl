using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class PgsqlTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<PgsqlTypeMapper> _instance = new(() => new PgsqlTypeMapper());

    public static new PgsqlTypeMapper Instance => _instance.Value;

    protected PgsqlTypeMapper()
    {
        AddOrReplace(DbType.Single, "REAL");
        AddOrReplace(DbType.Decimal, "NUMERIC");
        AddOrReplace(DbType.String, "TEXT");
        AddOrReplace(DbType.AnsiString, "VARCHAR");
    }
}
