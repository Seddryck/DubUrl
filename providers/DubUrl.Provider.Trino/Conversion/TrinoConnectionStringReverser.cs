using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Conversion.Implementation;

internal static class TrinoConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        p.TakeHost("Host");
        p.TakePort("Port");
        var catalog = p.Take("Catalog");
        var schema = p.Take("Schema");
        if (catalog.Length > 0 || schema.Length > 0) p.Segments.Add(catalog);
        if (schema.Length > 0) p.Segments.Add(schema);
        p.TakeCredentials("User", "Password");
        return p.ToUri(scheme);
    }
}
