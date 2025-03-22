using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class DaxTypeMapper : BaseTypeMapper
{
    private static readonly Lazy<DaxTypeMapper> _instance = new(() => new DaxTypeMapper());

    public static DaxTypeMapper Instance => _instance.Value;

    protected DaxTypeMapper()
    {
        AddOrReplace(DbType.Boolean, "BOOLEAN");
        AddOrReplace(DbType.SByte, "INTEGER");
        AddOrReplace(DbType.Int16, "INTEGER");
        AddOrReplace(DbType.Int64, "INTEGER");
        AddOrReplace(DbType.Single, "DOUBLE");
        AddOrReplace(DbType.Double, "DOUBLE");
        AddOrReplace(DbType.VarNumeric, "DECIMAL");
        AddOrReplace(DbType.String, "STRING");
        AddOrReplace(DbType.StringFixedLength, "STRING");
        AddOrReplace(DbType.AnsiString, "STRING");
        AddOrReplace(DbType.DateTime, "DATETIME");
        AddOrReplace(DbType.Time, "TIME");
        AddOrReplace(DbType.DateTimeOffset, "DATETIME");
        AddOrReplace(DbType.Guid, "STRING");
        AddOrReplace(DbType.Object, "VARIANT");
    }
}
