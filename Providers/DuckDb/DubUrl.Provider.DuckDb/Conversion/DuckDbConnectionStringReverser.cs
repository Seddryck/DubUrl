using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.DuckDb.Conversion;

internal static class DuckDbConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source, string databaseKeyword = "DataSource")
    {
        var p = new ConnectionStringParts(source);
        var path = p.Take(databaseKeyword);
        if (path != ":memory:")
        {
            p.Host = "localhost";
            p.Segments.Add(path);
        }
        return p.ToUri(scheme);
    }
}
