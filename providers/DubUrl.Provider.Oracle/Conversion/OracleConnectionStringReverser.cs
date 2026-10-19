using DubUrl.Conversion;
using DubUrl.Parsing;
using System.Data.Common;

namespace DubUrl.Provider.Oracle.Conversion;

internal static class OracleConnectionStringReverser
{
    public static ConnectionUri Convert(SchemeKey scheme, DbConnectionStringBuilder source)
    {
        var p = new ConnectionStringParts(source);
        var dataSource = p.Take("DATA SOURCE");
        // Preserve full descriptors and Easy Connect strings as provider options.
        if (Uri.CheckHostName(dataSource) != UriHostNameType.Unknown) p.Host = dataSource;
        else { p.Host = "localhost"; p.Options["DATA SOURCE"] = dataSource; }
        p.TakeCredentials("USER ID", "PASSWORD");
        return p.ToUri(scheme);
    }
}
