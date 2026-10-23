using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.DuckDb.Conversion;

internal static class DuckDbConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        var path = p.Take("DataSource");
        if (path != ":memory:")
        {
            p.Host = "localhost";
            p.Segments.Add(path);
        }
        return p.ToUri(scheme);
    }
}
