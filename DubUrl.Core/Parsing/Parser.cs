using System.Web;

namespace DubUrl.Parsing;

internal class Parser : IParser
{
    public virtual ConnectionUri Parse(string url)
        => TryParse(url, out var connectionUri, out var diagnostic)
            ? connectionUri!
            : throw new ConnectionUriParseException(diagnostic!);

    public virtual bool TryParse(string url, out ConnectionUri? connectionUri)
        => TryParse(url, out connectionUri, out _);

    public virtual bool TryParse(
        string url,
        out ConnectionUri? connectionUri,
        out ConnectionUriDiagnostic? diagnostic)
    {
        connectionUri = null;
        diagnostic = null;

        if (string.IsNullOrWhiteSpace(url))
            return Fail(ConnectionUriDiagnosticCode.EmptyInput, "The connection URI cannot be empty.", out diagnostic);

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return Fail(ConnectionUriDiagnosticCode.InvalidUri, "The connection URI is not a valid absolute URI.", out diagnostic);

        try
        {
            var (username, password) = ParseUserInfo(uri.UserInfo);
            var segments = uri.Segments
                .Skip(1)
                .Select(segment => DecodeUriComponent(segment.TrimEnd('/')))
                .ToArray();

            if (!TryParseOptions(uri.Query, out var options, out diagnostic))
                return false;

            var scheme = SchemeKey.Parse(uri.Scheme);
            connectionUri = new ConnectionUri
            {
                Scheme = scheme,
                Host = uri.Host,
                Port = uri.IsDefaultPort ? 0 : uri.Port,
                Username = username,
                Password = password,
                Segments = segments,
                Options = options,
            };
            return true;
        }
        catch (UriFormatException)
        {
            return Fail(
                ConnectionUriDiagnosticCode.InvalidEncoding,
                "The connection URI contains invalid percent-encoding.",
                out diagnostic);
        }
        catch (ArgumentException)
        {
            return Fail(ConnectionUriDiagnosticCode.InvalidUri, "The connection URI contains invalid syntax.", out diagnostic);
        }
    }

    private static (string Username, string Password) ParseUserInfo(string userInfo)
    {
        if (string.IsNullOrEmpty(userInfo))
            return (string.Empty, string.Empty);

        var separatorIndex = userInfo.IndexOf(':');
        var username = separatorIndex >= 0 ? userInfo[..separatorIndex] : userInfo;
        var password = separatorIndex >= 0 ? userInfo[(separatorIndex + 1)..] : string.Empty;
        return (DecodeUriComponent(username), DecodeUriComponent(password));
    }

    private static string DecodeUriComponent(string value)
    {
        ValidatePercentEncoding(value);
        return Uri.UnescapeDataString(value);
    }

    private static bool TryParseOptions(
        string query,
        out IReadOnlyDictionary<string, string> options,
        out ConnectionUriDiagnostic? diagnostic)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        options = parsed;
        diagnostic = null;

        if (string.IsNullOrEmpty(query))
            return true;

        foreach (var item in query[1..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = item.IndexOf('=');
            var rawKey = separatorIndex >= 0 ? item[..separatorIndex] : item;
            var rawValue = separatorIndex >= 0 ? item[(separatorIndex + 1)..] : string.Empty;
            ValidatePercentEncoding(rawKey);
            ValidatePercentEncoding(rawValue);
            var key = HttpUtility.UrlDecode(rawKey);
            var value = HttpUtility.UrlDecode(rawValue);

            if (!parsed.TryAdd(key, value))
                return Fail(
                    ConnectionUriDiagnosticCode.DuplicateOption,
                    $"The connection URI contains the duplicate option '{key}'.",
                    out diagnostic);
        }

        return true;
    }

    private static void ValidatePercentEncoding(string value)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] != '%')
                continue;
            if (index + 2 >= value.Length || !IsHex(value[index + 1]) || !IsHex(value[index + 2]))
                throw new UriFormatException();
            index += 2;
        }
    }

    private static bool IsHex(char value)
        => value is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private static bool Fail(
        ConnectionUriDiagnosticCode code,
        string message,
        out ConnectionUriDiagnostic? diagnostic)
    {
        diagnostic = new(code, message);
        return false;
    }
}
