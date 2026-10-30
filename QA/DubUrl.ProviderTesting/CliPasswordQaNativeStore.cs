using DubUrl.Authentication;
using DubUrl.Cli;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

/// <summary>Provisions and removes only a random named entry through the production store interface.</summary>
internal sealed class CliPasswordQaNativeStore : IAsyncDisposable
{
    internal string Reference { get; } = "qa-" + Guid.NewGuid().ToString("N");
    private readonly ServiceProvider services = CliComposition.CreateDefault();
    private INamedCredentialStore Store => services.GetRequiredService<INamedCredentialStore>();

    internal static async Task<CliPasswordQaNativeStore> CreateWindowsAsync(UsernamePasswordCredentials credentials)
    {
        if (!OperatingSystem.IsWindows()) Assert.Ignore("Windows Credential Manager cannot run on this QA operating system.");
        return await CreateAsync(credentials);
    }

    internal static async Task<CliPasswordQaNativeStore> CreateLinuxAsync(UsernamePasswordCredentials credentials)
    {
        if (!OperatingSystem.IsLinux()) Assert.Ignore("Linux Secret Service cannot run on this QA operating system.");
        return await CreateAsync(credentials);
    }

    private static async Task<CliPasswordQaNativeStore> CreateAsync(UsernamePasswordCredentials credentials)
    {
        var entry = new CliPasswordQaNativeStore();
        try
        {
            await entry.Store.AddAsync(entry.Reference, credentials, false);
            return entry;
        }
        catch (CredentialStoreException)
        {
            await entry.services.DisposeAsync();
            Assert.Ignore("This runner has no accessible unlocked native store for CredOS; no store is installed or unlocked by QA.");
            throw;
        }
        catch
        {
            await entry.services.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try { await Store.RemoveAsync(Reference); }
        finally { await services.DisposeAsync(); }
    }
}
