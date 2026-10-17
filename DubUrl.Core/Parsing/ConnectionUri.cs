using System.Collections.ObjectModel;
using System.Text;

namespace DubUrl.Parsing;

/// <summary>
/// Immutable, provider-neutral representation of a database connection URI.
/// Credentials and path segments are percent-decoded exactly once using URI semantics;
/// literal plus signs are preserved. Query option keys and values use form/query decoding,
/// and duplicate option names are rejected using ordinal case-insensitive comparison.
/// </summary>
public sealed class ConnectionUri : IEquatable<ConnectionUri>
{
    private IReadOnlyList<string> segments = Array.Empty<string>();
    private IReadOnlyDictionary<string, string> options =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;

    public SchemeKey Scheme { get; init; } = new([]);

    public IReadOnlyList<string> Segments
    {
        get => segments;
        init => segments = Array.AsReadOnly([.. value ?? throw new ArgumentNullException(nameof(value))]);
    }

    public IReadOnlyDictionary<string, string> Options
    {
        get => options;
        init => options = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(value ?? throw new ArgumentNullException(nameof(value)), StringComparer.OrdinalIgnoreCase));
    }

    public static ConnectionUri Parse(string value) => new Parser().Parse(value);

    public static bool TryParse(string value, out ConnectionUri? connectionUri)
        => new Parser().TryParse(value, out connectionUri);

    public static bool TryParse(
        string value,
        out ConnectionUri? connectionUri,
        out ConnectionUriDiagnostic? diagnostic)
        => new Parser().TryParse(value, out connectionUri, out diagnostic);

    public ConnectionUri WithOption(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var updated = new Dictionary<string, string>(Options, StringComparer.OrdinalIgnoreCase)
        {
            [key] = value,
        };
        return new ConnectionUri
        {
            Scheme = Scheme,
            Host = Host,
            Port = Port,
            Username = Username,
            Password = Password,
            Segments = Segments,
            Options = updated,
        };
    }

    /// <summary>Formats the URI with its password redacted.</summary>
    public override string ToString() => ToString(redactPassword: true);

    public string ToString(bool redactPassword)
    {
        var builder = new StringBuilder().Append(Scheme).Append("://");
        if (!string.IsNullOrEmpty(Username) || !string.IsNullOrEmpty(Password))
        {
            builder.Append(Uri.EscapeDataString(Username));
            if (!string.IsNullOrEmpty(Password))
                builder.Append(':').Append(redactPassword ? "***" : Uri.EscapeDataString(Password));
            builder.Append('@');
        }

        builder.Append(Host.Contains(':') ? $"[{Host}]" : Host);
        if (Port > 0)
            builder.Append(':').Append(Port);

        foreach (var segment in Segments)
            builder.Append('/').Append(Uri.EscapeDataString(segment));

        if (Options.Count > 0)
        {
            builder.Append('?');
            builder.AppendJoin('&', Options.Select(option =>
                $"{Uri.EscapeDataString(option.Key)}={Uri.EscapeDataString(option.Value)}"));
        }

        return builder.ToString();
    }

    public bool Equals(ConnectionUri? other)
        => other is not null
        && Scheme.Equals(other.Scheme)
        && StringComparer.OrdinalIgnoreCase.Equals(Host, other.Host)
        && Port == other.Port
        && Username == other.Username
        && Password == other.Password
        && Segments.SequenceEqual(other.Segments, StringComparer.Ordinal)
        && Options.Count == other.Options.Count
        && Options.All(option => other.Options.TryGetValue(option.Key, out var value) && value == option.Value);

    public override bool Equals(object? obj) => Equals(obj as ConnectionUri);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Scheme);
        hash.Add(Host, StringComparer.OrdinalIgnoreCase);
        hash.Add(Port);
        hash.Add(Username, StringComparer.Ordinal);
        hash.Add(Password, StringComparer.Ordinal);
        foreach (var segment in Segments)
            hash.Add(segment, StringComparer.Ordinal);
        foreach (var option in Options.OrderBy(option => option.Key, StringComparer.OrdinalIgnoreCase))
        {
            hash.Add(option.Key, StringComparer.OrdinalIgnoreCase);
            hash.Add(option.Value, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}
