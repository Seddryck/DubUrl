using System.Data.Common;
using DubUrl.BulkCopy;
using DubUrl.Parsing;

namespace DubUrl.Authentication;

/// <summary>A native provider adapter that prepares closed file-secret connections.</summary>
public interface IFileSecretConnectionFactory
{
    DbConnection Create(string connectionString, FileSecretCredentials? credentials);
}

/// <summary>Receives a caller-owned source without resolving it during preparation.</summary>
public interface IFileSecretConnection
{
    void ConfigureFileSecret(IFileSecretCredentialSource source, AuthenticationContext context);
}

/// <summary>Loads a native adapter only when an encrypted connection is prepared.</summary>
public sealed class FileSecretConnectionCapability(string optionName, string assemblyName, string typeName)
{
    public string OptionName { get; } = optionName;

    internal DbConnection Create(DbProviderFactory provider, string connectionString, FileSecretCredentials? credentials)
    {
        var dependencies = BulkCopyDependencyResolver.Default;
        using var scope = dependencies.EnterScope(provider.GetType().Assembly);
        var type = dependencies.ResolveType(provider.GetType().Assembly, assemblyName, typeName);
        var adapter = Activator.CreateInstance(type) as IFileSecretConnectionFactory
            ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.UnsupportedAuthentication);
        return adapter.Create(connectionString, credentials);
    }

    /// <summary>Validates URL-only file authentication and extracts a once-decoded secret.</summary>
    public static FileSecretCredentials? GetUrlCredentials(ConnectionUri uri, string optionName)
    {
        var options = uri.Options.Where(o => IsOption(o.Key, optionName)).ToArray();
        if (options.Length == 0) return null;
        if (options.Length != 1) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        if (string.IsNullOrEmpty(options[0].Value))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
        // Reuse the shared conflict policy after consuming the file-secret option itself.
        var clean = new ConnectionUri
        {
            Scheme = uri.Scheme, Host = uri.Host, Port = uri.Port, Segments = uri.Segments,
            Username = uri.Username, Password = uri.Password, HasUsername = uri.HasUsername, HasPassword = uri.HasPassword,
            Options = uri.Options.Where(o => !IsOption(o.Key, optionName)).ToDictionary(o => o.Key, o => o.Value)
        };
        AuthenticationConfiguration.Validate(clean, ConnectionAuthentication.FilePassword(""));
        return new(options[0].Value);
    }

    public static bool IsOption(string key, string optionName)
        => Normalize(key) == Normalize(optionName);

    private static string Normalize(string key)
        => new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class FileSecretConnectionAttribute(string optionName, string assemblyName, string typeName) : Attribute
{
    internal FileSecretConnectionCapability Create() => new(optionName, assemblyName, typeName);
}
