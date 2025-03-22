using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class TSqlTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<TSqlTypeMapper> _instance = new(() => new TSqlTypeMapper());

    public static new TSqlTypeMapper Instance => _instance.Value;

    protected TSqlTypeMapper()
    {
        AddOrReplace(DbType.Boolean, "BIT");
        AddOrReplace(DbType.Single, "REAL");
        AddOrReplace(DbType.Double, "FLOAT(53)");
        AddOrReplace(DbType.Decimal, "DECIMAL");
        AddOrReplace(DbType.String, "NVARCHAR");
        AddOrReplace(DbType.StringFixedLength, "NCHAR");
        AddOrReplace(DbType.AnsiString, "VARCHAR");
        AddOrReplace(DbType.AnsiStringFixedLength, "CHAR");
        AddOrReplace(DbType.DateTime, "DATETIME2");
        AddOrReplace(DbType.DateTimeOffset, "DATETIMEOFFSET");
        AddOrReplace(DbType.Guid, "UNIQUEIDENTIFIER");
    }
}
