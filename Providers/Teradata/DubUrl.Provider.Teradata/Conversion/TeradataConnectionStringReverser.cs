using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.Teradata.Conversion;

internal static class TeradataConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        p.TakeHost("Data Source");
        p.TakePort("Port Number");
        var database = p.Take("Database");
        if (database.Length > 0) p.Segments.Add(database);
        p.TakeCredentials("User Id", "Password");
        return p.ToUri(scheme);
    }
}
