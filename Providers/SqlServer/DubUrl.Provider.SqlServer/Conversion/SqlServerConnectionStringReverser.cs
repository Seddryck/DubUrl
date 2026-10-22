using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.SqlServer.Conversion;

internal static class SqlServerConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        var server = p.Take("Data Source");
        var endpoint = server.Split(',');
        var instance = endpoint[0].Split('\\');
        if (endpoint.Length > 2 || instance.Length > 2) throw new NotSupportedException();
        p.Host = instance[0].Trim('[', ']');
        if (instance.Length == 2) p.Segments.Add(instance[1]);
        if (endpoint.Length == 2) p.Port = int.Parse(endpoint[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
        p.Segments.Add(p.Take("Initial Catalog"));
        p.TakeCredentials("User ID", "Password");
        if (Uri.CheckHostName(p.Host) == UriHostNameType.Unknown)
        {
            p.Host = "localhost";
            p.Options["Data Source"] = server;
        }
        return p.ToUri(scheme);
    }
}
