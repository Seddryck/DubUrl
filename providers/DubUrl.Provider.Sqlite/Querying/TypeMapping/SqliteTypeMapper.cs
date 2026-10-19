using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class SqliteTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<SqliteTypeMapper> _instance = new(() => new SqliteTypeMapper());

    public static new SqliteTypeMapper Instance => _instance.Value;

    protected SqliteTypeMapper()
    {
        AddOrReplace(DbType.Boolean, "INTEGER");
        AddOrReplace(DbType.SByte, "INTEGER");
        AddOrReplace(DbType.Int16, "INTEGER"); 
        AddOrReplace(DbType.Int32, "INTEGER");
        AddOrReplace(DbType.Int64, "INTEGER");
        AddOrReplace(DbType.Single, "REAL");
        AddOrReplace(DbType.Double, "REAL");
        AddOrReplace(DbType.Decimal, "NUMERIC");
        AddOrReplace(DbType.String, "TEXT");
        AddOrReplace(DbType.StringFixedLength, "TEXT");
        AddOrReplace(DbType.AnsiString, "TEXT");
        AddOrReplace(DbType.Date, "TEXT");
        AddOrReplace(DbType.DateTime, "TEXT");
        AddOrReplace(DbType.DateTimeOffset, "TEXT");
        AddOrReplace(DbType.Guid, "TEXT");
    }
}
