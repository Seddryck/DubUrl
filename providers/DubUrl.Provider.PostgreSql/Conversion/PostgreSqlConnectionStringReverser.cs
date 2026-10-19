using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Conversion.Implementation;

internal static class PostgreSqlConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        p.TakeHost("Host");
        p.TakePort("Port");
        p.Segments.Add(p.Take("Database"));
        p.TakeCredentials("Username", "Password");
        return p.ToUri(scheme);
    }
}
