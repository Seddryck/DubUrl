using DubUrl.Authentication;

namespace DubUrl.Cli;

/// <summary>The only adapter that depends on CredOS; callers use INamedCredentialStore.</summary>
internal sealed class CredOsNamedCredentialStore : INamedCredentialStore
{
    private readonly CredOS.ICredentialStore store;

    public CredOsNamedCredentialStore() : this(new CredOS.CredentialStore("duburl")) { }

    internal CredOsNamedCredentialStore(CredOS.ICredentialStore store) => this.store = store;

    public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        => ExecuteAsync(() => store.ExistsAsync(CredentialName.Normalize(name), cancellationToken));

    public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        => ExecuteAsync(async () =>
        {
            var credentials = await store.ReadAsync(CredentialName.Normalize(reference), cancellationToken).ConfigureAwait(false);
            return new UsernamePasswordCredentials(credentials.Username, credentials.Password);
        });

    public async ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default)
    {
        CredentialInput.ValidateCredentials(credentials);
        await ExecuteAsync(async () =>
        {
            await store.AddAsync(CredentialName.Normalize(name), new CredOS.UsernamePasswordCredentials(credentials.Username!, credentials.Password!), replace, cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    public async ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
        => await ExecuteAsync(async () =>
        {
            await store.RemoveAsync(CredentialName.Normalize(name), cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    private static async ValueTask<T> ExecuteAsync<T>(Func<ValueTask<T>> operation)
    {
        try { return await operation().ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch (ArgumentException) { throw; }
        catch (CredOS.CredentialAlreadyExistsException) { throw new CredentialStoreException(alreadyExists: true); }
        catch { throw new CredentialStoreException(); }
    }
}
