using System.Data.Common;

namespace DubUrl;

internal static class TlsOptionValidation
{
    internal static string Normalize(string key) => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    internal static void Require(DbConnectionStringBuilder supplied, string keyword, params string[] allowed)
    {
        var normalized = Normalize(keyword);
        foreach (string key in supplied.Keys)
        {
            if (Normalize(key) != normalized) continue;
            var value = Convert.ToString(supplied[key]) ?? "";
            if (!IsAllowed(normalized, value, allowed))
                throw new ArgumentException("Explicit TLS configuration conflicts with a native URL option.");
        }
    }

    private static bool IsAllowed(string keyword, string value, string[] allowed)
        => keyword is "rootcertificate" or "sslca"
            ? allowed.Any(path => PathsEqual(path, value))
            : allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    private static bool PathsEqual(string first, string second)
        => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    internal static void RejectExplicit(DbConnectionStringBuilder supplied, params string[] keywords)
    {
        if (supplied.Keys.Cast<string>().Any(key => keywords.Any(keyword => Normalize(keyword) == Normalize(key))))
            throw new ArgumentException("Explicit TLS configuration conflicts with a native verification override.");
    }
}
