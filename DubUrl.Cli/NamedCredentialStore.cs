using System.Text;
using DubUrl.Authentication;

namespace DubUrl.Cli;

/// <summary>Manages one username/password pair per portable name, separately from legacy OS references.</summary>
public interface INamedCredentialStore : IOsCredentialStore
{
    ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default);
    ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default);
    ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default);
}

public static class CredentialName
{
    public static string Normalize(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 128 || !char.IsAsciiLetterOrDigit(name[0])
            || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Credential names must contain 1–128 ASCII letters, digits, dots, hyphens or underscores, starting with a letter or digit.");
        return name.ToLowerInvariant();
    }
}

public sealed class CredentialStoreException : Exception
{
    public CredentialStoreException(bool alreadyExists = false)
        : base(alreadyExists ? "Credential already exists. Use --replace to update it."
            : "OS credential store unavailable or access denied. Ensure the current user's store is installed and unlocked (on Linux, libsecret and a Secret Service session are required). No plaintext fallback is used.")
        => AlreadyExists = alreadyExists;
    public bool AlreadyExists { get; }
}

internal static class CredentialInput
{
    internal static void ValidateCredentials(UsernamePasswordCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.Username) || credentials.Username.Length > 512
            || credentials.Username.Any(char.IsControl) || credentials.Password is null
            || credentials.Password.Length > 1280)
            throw new ArgumentException("A username (up to 512 characters) and password (up to 1280 UTF-16 code units) are required.");
        var unicode = new UTF8Encoding(false, true);
        _ = unicode.GetByteCount(credentials.Username);
        _ = unicode.GetByteCount(credentials.Password);
    }
}
