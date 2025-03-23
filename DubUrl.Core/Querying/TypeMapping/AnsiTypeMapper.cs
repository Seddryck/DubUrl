using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class AnsiTypeMapper : BaseTypeMapper
{
    private static readonly Lazy<AnsiTypeMapper> _instance = new(() => new AnsiTypeMapper());

    public static AnsiTypeMapper Instance => _instance.Value;

    protected AnsiTypeMapper()
    {
        AddOrReplace(DbType.Boolean, "BOOLEAN");
        AddOrReplace(DbType.SByte, "TINYINT");
        AddOrReplace(DbType.Int16, "SMALLINT");
        AddOrReplace(DbType.Int32, "INTEGER");
        AddOrReplace(DbType.Int64, "BIGINT");
        AddOrReplace(DbType.Single, "FLOAT");
        AddOrReplace(DbType.Double, "DOUBLE PRECISION");
        AddOrReplace(DbType.Decimal, "DECIMAL");
        AddOrReplace(DbType.String, "VARCHAR");
        AddOrReplace(DbType.StringFixedLength, "CHAR");
        AddOrReplace(DbType.AnsiString, "CHARACTER VARYING");
        AddOrReplace(DbType.Date, "DATE");
        AddOrReplace(DbType.DateTime, "TIMESTAMP");
        AddOrReplace(DbType.DateTimeOffset, "TIMESTAMP WITH TIMEZONE");
        AddOrReplace(DbType.Guid, "CHAR(36)");
    }
}
