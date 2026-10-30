using System.CommandLine;
using System.CommandLine.Parsing;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

public interface IHiddenPasswordPrompt
{
    ValueTask<string> ReadAsync(CancellationToken cancellationToken = default);
}

internal sealed class CliAuthentication : IDisposable
{
    private readonly CliOAuthOptions oauth = new();
    private readonly CliCertificateOptions certificate = new();
    private IDisposable? oauthLifetime;
    internal int AcquisitionTimeoutSeconds => oauth.AcquisitionTimeoutSeconds;
    private readonly Option<bool> prompt = new("--password-prompt") { Description = "Explicitly read a hidden password from the terminal." };
    private readonly Option<string?> named = new("--credential") { Description = "Read a named credential saved with 'duburl credential add'." };
    private readonly Option<string?> username = new("--username") { Description = "Username for the selected external credential source." };
    private readonly Option<bool> osIdentity = new("--os-identity") { Description = "Use the current process identity or existing ticket cache through the selected driver." };
    public void AddTo(Command command) { command.Options.Add(prompt); command.Options.Add(named); command.Options.Add(username); command.Options.Add(osIdentity); oauth.AddTo(command); certificate.AddTo(command); }
    public ConnectionUrl Configure(IServiceProvider services, ParseResult result, string url)
    {
        var password = new PasswordSelection(result.GetValue(prompt), result.GetValue(named), result.GetValue(username));
        var useOsIdentity = result.GetValue(osIdentity);
        var useCertificate = certificate.IsSelected(result);
        var useOAuth = oauth.IsSelected(result);
        if (useCertificate && (useOsIdentity || useOAuth))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        ValidateSelection(useOsIdentity, password.Count, password.Username);
        if (password.Credential is not null) password = password with { Credential = CredentialName.Normalize(password.Credential) };
        var target = new ConnectionUrl(url, services.GetRequiredService<ISchemeRegistry>());
        if (useOsIdentity) target = target.WithAuthentication(ConnectionAuthentication.OperatingSystemIdentity());
        if (useCertificate) target = certificate.Configure(services, result, target, password.Count > 0 ? password.CreateSource(services) : null);
        else if (password.Count > 0)
            target = target.WithAuthentication(ConnectionAuthentication.UsernamePassword(password.CreateSource(services)));
        if (useOAuth)
            target = ConfigureOAuth(services, result, target, password, useOsIdentity);
        target.ValidateAuthentication();
        return target;
    }

    private ConnectionUrl ConfigureOAuth(IServiceProvider services, ParseResult result, ConnectionUrl target, PasswordSelection password, bool useOsIdentity)
    {
        if (password.Count > 0 || password.Username is not null || useOsIdentity)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        var configured = oauth.Configure(services, result, target, out var lifetime);
        oauthLifetime = lifetime;
        return configured;
    }

    private sealed record PasswordSelection(bool Prompt, string? Credential, string? Username)
    {
        internal int Count => (Prompt ? 1 : 0) + (Credential is not null ? 1 : 0);
        internal Source CreateSource(IServiceProvider services)
            => new(services, Prompt, Credential, Username);
    }

    private static void ValidateSelection(bool useOsIdentity, int passwordSources, string? user)
    {
        var sourceCount = passwordSources + (useOsIdentity ? 1 : 0);
        if (sourceCount > 1 || (user is not null && (useOsIdentity || sourceCount == 0)))
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    }

    public void Dispose() { oauthLifetime?.Dispose(); certificate.Dispose(); }
    internal static int ExitCode(ConnectionAuthenticationException error)
        => error.Code is AuthenticationDiagnosticCodes.CredentialConflict or AuthenticationDiagnosticCodes.UnsupportedAuthentication
            or AuthenticationDiagnosticCodes.MissingCredentials or AuthenticationDiagnosticCodes.SynchronousCredentialsRequired
            ? CliExitCodes.InvalidInput : CliExitCodes.ConnectionCheckFailed;

    private sealed class Source(IServiceProvider services, bool prompt, string? reference, string? username) : IUsernamePasswordCredentialSource
    {
        public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
            => prompt ? ResolvePromptAsync(context, cancellationToken) : ResolveStoredAsync(cancellationToken);

        private async ValueTask<UsernamePasswordCredentials> ResolvePromptAsync(AuthenticationContext context, CancellationToken cancellationToken)
        {
            if (!services.GetRequiredService<ICliConsole>().IsInteractive)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InteractionRequired);
            if (username is null && context.Username is null) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            var password = await services.GetRequiredService<IHiddenPasswordPrompt>().ReadAsync(cancellationToken).ConfigureAwait(false);
            return new(username, password);
        }

        private async ValueTask<UsernamePasswordCredentials> ResolveStoredAsync(CancellationToken cancellationToken)
        {
            var selectedReference = reference ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed);
            UsernamePasswordCredentials stored;
            try
            {
                var store = services.GetRequiredService<INamedCredentialStore>();
                stored = await store.ReadAsync(selectedReference, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
            if (username is not null && stored.Username is not null && username != stored.Username)
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
            return new(username ?? stored.Username, stored.Password);
        }
    }
}

internal sealed class TerminalPasswordPrompt : IHiddenPasswordPrompt
{
    public async ValueTask<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InteractionRequired);
        await Console.Error.WriteAsync("Password: ").ConfigureAwait(false);
        var characters = new List<char>();
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Console.KeyAvailable) { await Task.Delay(30, cancellationToken).ConfigureAwait(false); continue; }
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) return new string(characters.ToArray());
                if (key.Key == ConsoleKey.Escape) throw new OperationCanceledException(cancellationToken);
                if (key.Key == ConsoleKey.Backspace) { if (characters.Count > 0) characters.RemoveAt(characters.Count - 1); }
                else if (!char.IsControl(key.KeyChar)) characters.Add(key.KeyChar);
            }
        }
        finally { characters.Clear(); await Console.Error.WriteLineAsync().ConfigureAwait(false); }
    }
}
