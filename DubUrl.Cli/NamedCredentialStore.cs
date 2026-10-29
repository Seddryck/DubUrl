using System.Security.Cryptography;
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

internal interface INativeNamedCredentialStore
{
    bool Exists(string name, CancellationToken token);
    UsernamePasswordCredentials Read(string name, CancellationToken token);
    void Write(string name, UsernamePasswordCredentials credentials, CancellationToken token);
    void Remove(string name, CancellationToken token);
}

internal sealed class NamedCredentialStore(bool interactive) : INamedCredentialStore
{
    private INativeNamedCredentialStore Backend
    {
        get
        {
            if (OperatingSystem.IsWindows()) return new WindowsNamedCredentialStore();
            if (OperatingSystem.IsMacOS()) return new MacNamedCredentialStore(interactive);
            if (OperatingSystem.IsLinux()) return new LinuxNamedCredentialStore();
            throw new CredentialStoreException();
        }
    }

    public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(name, (backend, normalized) => backend.Exists(normalized, cancellationToken), cancellationToken);

    public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        => ExecuteAsync(reference, (backend, normalized) => backend.Read(normalized, cancellationToken), cancellationToken);

    public async ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default)
    {
        ValidateCredentials(credentials);
        await ExecuteAsync(name, (backend, normalized) =>
        {
            if (backend.Exists(normalized, cancellationToken) && !replace) throw new CredentialStoreException(alreadyExists: true);
            cancellationToken.ThrowIfCancellationRequested();
            backend.Write(normalized, credentials, cancellationToken);
            return true;
        }, cancellationToken).ConfigureAwait(false);
    }

    internal static void ValidateCredentials(UsernamePasswordCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        if (string.IsNullOrWhiteSpace(credentials.Username) || credentials.Username.Length > 513
            || credentials.Username.Any(char.IsControl) || credentials.Password is null
            || Encoding.Unicode.GetByteCount(credentials.Password) > 2560)
            throw new ArgumentException("A username (up to 513 characters) and password (up to 1280 UTF-16 code units) are required.");
    }

    public async ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
        => await ExecuteAsync(name, (backend, normalized) => { backend.Remove(normalized, cancellationToken); return true; }, cancellationToken).ConfigureAwait(false);

    private ValueTask<T> ExecuteAsync<T>(string name, Func<INativeNamedCredentialStore, string, T> action, CancellationToken token)
    {
        var normalized = CredentialName.Normalize(name);
        return new(Task.Run(() =>
        {
            try
            {
                // Serialize cooperating CLI processes, including create-if-absent checks on WinCred.
                var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/" + normalized)));
                using var mutex = new Mutex(false, "DubUrl.Credential." + key);
                var acquired = false;
                try
                {
                    try
                    {
                        // Unix does not support WaitAny with a named mutex and cancellation event.
                        acquired = mutex.WaitOne(100);
                        while (!acquired)
                        {
                            token.ThrowIfCancellationRequested();
                            acquired = mutex.WaitOne(100);
                        }
                    }
                    catch (AbandonedMutexException) { acquired = true; }
                    token.ThrowIfCancellationRequested();
                    return action(Backend, normalized);
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
            catch (OperationCanceledException) { throw; }
            catch (CredentialStoreException) { throw; }
            catch { throw new CredentialStoreException(); }
        }, token));
    }
}
