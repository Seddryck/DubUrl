using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public class FirebirdSqlTypeMapper : AnsiTypeMapper
{
    private static readonly Lazy<FirebirdSqlTypeMapper> _instance = new(() => new FirebirdSqlTypeMapper());

    protected static new FirebirdSqlTypeMapper Instance => _instance.Value;

    protected FirebirdSqlTypeMapper()
    {
        AddOrReplace(DbType.SByte, "SMALLINT");
        AddOrReplace(DbType.Decimal, "DECIMAL");
        AddOrReplace(DbType.AnsiString, "VARCHAR");
    }
}
