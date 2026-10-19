using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class DuckDbTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<DuckDbTypeMapper> _instance = new(() => new DuckDbTypeMapper());

    public static new DuckDbTypeMapper Instance => _instance.Value;

    protected DuckDbTypeMapper()
    {
        AddOrReplace(DbType.Double, "DOUBLE");
        AddOrReplace(DbType.Decimal, "DECIMAL");
        AddOrReplace(DbType.AnsiString, "VARCHAR");
        AddOrReplace(DbType.Guid, "UUID");
    }
}
