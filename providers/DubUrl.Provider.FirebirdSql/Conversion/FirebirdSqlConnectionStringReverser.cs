using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Conversion.Implementation;

internal static class FirebirdSqlConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        p.TakeHost("DataSource", "Data Source");
        p.TakePort("Port");
        p.Segments.Add(p.Take("Database"));
        p.TakeCredentials("User ID", "Password");
        return p.ToUri(scheme);
    }
}
