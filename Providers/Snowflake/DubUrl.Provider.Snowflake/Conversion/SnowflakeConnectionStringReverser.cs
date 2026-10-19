using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.Snowflake.Conversion;

internal static class SnowflakeConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        p.TakeHost("ACCOUNT");
        var database = p.Take("DB");
        var schema = p.Take("SCHEMA");
        if (database.Length > 0 || schema.Length > 0) p.Segments.Add(database);
        if (schema.Length > 0) p.Segments.Add(schema);
        p.TakeCredentials("USER", "PASSWORD");
        return p.ToUri(scheme);
    }
}
