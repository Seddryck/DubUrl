using DubUrl.Parsing;
using System.Data.Common;
using System.Globalization;

namespace DubUrl.Conversion;

/// <summary>Shared extraction helpers; all keyword and URL layout choices belong to providers.</summary>
public sealed class ConnectionStringParts
{
    private readonly DbConnectionStringBuilder source;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public List<string> Segments { get; } = [];
    public Dictionary<string, string> Options { get; } = new(StringComparer.OrdinalIgnoreCase);

    public ConnectionStringParts(DbConnectionStringBuilder source)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.source = source;
        // Preserve provider grammar (including ODBC braces) and exclude implicit defaults.
        foreach (string key in source.Keys)
            if (source.ShouldSerialize(key))
                Options.Add(key, Text(source[key]));
    }

    public string Take(params string[] aliases)
    {
        foreach (var key in aliases)
            if (Options.Remove(key, out var value))
                return value;
        foreach (var key in aliases)
            if (source.TryGetValue(key, out var value))
                return Text(value);
        return string.Empty;
    }

    public void TakeCredentials(string username, string password)
        => (Username, Password) = (Take(username), Take(password));

    public void TakePort(params string[] aliases)
    {
        var value = Take(aliases);
        if (string.IsNullOrEmpty(value))
            return;
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            || port is < 1 or > 65535)
            throw new NotSupportedException();
        Port = port;
    }

    public void TakeHost(params string[] aliases)
    {
        var value = Take(aliases);
        var host = value.Trim('[', ']');
        if (string.IsNullOrEmpty(host) || Uri.CheckHostName(host) != UriHostNameType.Unknown)
            Host = host;
        else
        {
            // Multi-host lists, sockets, and other provider syntax remain provider options.
            Host = "localhost";
            Options[aliases[0]] = value;
        }
    }

    public ConnectionUri ToUri(SchemeKey scheme) => new()
    {
        Scheme = scheme, Host = Host, Port = Port, Username = Username, Password = Password,
        Segments = Segments, Options = Options,
    };

    private static string Text(object? value) => System.Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
}
