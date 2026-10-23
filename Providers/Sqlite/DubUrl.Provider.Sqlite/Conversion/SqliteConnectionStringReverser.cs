using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.Sqlite.Conversion;

internal static class SqliteConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        var path = p.Take("Data Source");
        if (path != ":memory:")
        {
            p.Host = "localhost";
            p.Segments.Add(path);
        }
        return p.ToUri(scheme);
    }
}
