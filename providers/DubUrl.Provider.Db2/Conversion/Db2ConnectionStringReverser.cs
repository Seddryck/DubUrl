using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Conversion.Implementation;

internal static class Db2ConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        var endpoint = p.Take("Server");
        if (Uri.TryCreate("tcp://" + endpoint, UriKind.Absolute, out var address))
        { p.Host = address.Host.Trim('[', ']'); p.Port = address.Port > 0 ? address.Port : 0; }
        else throw new NotSupportedException();
        p.Segments.Add(p.Take("Database"));
        p.TakeCredentials("User ID", "Password");
        return p.ToUri(scheme);
    }
}
