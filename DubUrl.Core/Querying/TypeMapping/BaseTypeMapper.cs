using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Querying.TypeMapping;
public abstract class BaseTypeMapper : IDbTypeMapper
{
    private Dictionary<DbType, string> Mappings { get; } = [];

    public BaseTypeMapper()
    { }

    protected void AddOrReplace(DbType dbType, string sqlType)
    {
        if (!Mappings.TryAdd(dbType, sqlType))
            Mappings[dbType] = sqlType;
    }

    public IDictionary<string, object> ToDictionary()
        => Mappings.ToDictionary(kvp => kvp.Key.ToString(), kvp => (object)kvp.Value);
}
