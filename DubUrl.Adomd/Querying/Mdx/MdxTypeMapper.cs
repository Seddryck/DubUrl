using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class MdxTypeMapper : BaseTypeMapper
{
    private static readonly Lazy<MdxTypeMapper> _instance = new(() => new MdxTypeMapper());

    public static MdxTypeMapper Instance => _instance.Value;

    protected MdxTypeMapper()
    {
        AddOrReplace(DbType.Boolean, "Boolean");
        AddOrReplace(DbType.SByte, "SByte");
        AddOrReplace(DbType.Byte, "Byte");
        AddOrReplace(DbType.Int16, "Short");
        AddOrReplace(DbType.Int32, "Integer");
        AddOrReplace(DbType.Int64, "BigInteger");
        AddOrReplace(DbType.Single, "Single");
        AddOrReplace(DbType.Double, "Double");
        AddOrReplace(DbType.Decimal, "Double");
        AddOrReplace(DbType.VarNumeric, "Double");
        AddOrReplace(DbType.Currency, "Currency");
        AddOrReplace(DbType.String, "String");
        AddOrReplace(DbType.StringFixedLength, "String");
        AddOrReplace(DbType.AnsiString, "String");
        AddOrReplace(DbType.AnsiStringFixedLength, "String");
        AddOrReplace(DbType.Guid, "String");
        AddOrReplace(DbType.Xml, "String");
        AddOrReplace(DbType.Date, "Date");
        AddOrReplace(DbType.Time, "Date");
        AddOrReplace(DbType.DateTime, "Date");
        AddOrReplace(DbType.DateTime2, "Date");
        AddOrReplace(DbType.DateTimeOffset, "Date");
        AddOrReplace(DbType.Object, "Variant");
    }
}
