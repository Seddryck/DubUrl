using System.CommandLine;
using DubUrl.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal sealed class CliCertificateOptions : IDisposable
{
    private readonly Option<string?> certificate = new("--client-certificate") { Description = "Client certificate: PEM with --client-key, or a PFX/P12 bundle." };
    private readonly Option<string?> key = new("--client-key") { Description = "Matching PEM private-key file." };
    private readonly Option<string?> ca = new("--ca-certificate") { Description = "Optional PEM CA file for server verification; otherwise use normal trust." };
    private readonly Option<bool> prompt = new("--certificate-password-prompt") { Description = "Read a hidden key/bundle decryption password, separate from the database password." };
    private readonly Option<string?> store = new("--certificate-credential-store") { Description = "Read only the decryption password from an existing OS credential-store reference." };
    private CertificateOptions? lifetime;
    private Option[] Options => [certificate, key, ca, prompt, store];
    internal void AddTo(Command command) { foreach (var option in Options) command.Options.Add(option); }
    internal bool IsSelected(ParseResult result)
    {
        if (Options.Any(option => result.GetResult(option)?.IdentifierTokenCount > 1)) Conflict();
        var selected = result.GetValue(certificate) is not null;
        if (!selected && Options.Skip(1).Any(option => result.GetResult(option) is { Implicit: false })) Conflict();
        return selected;
    }
    internal ConnectionUrl Configure(IServiceProvider services, ParseResult result, ConnectionUrl target, IUsernamePasswordCredentialSource? supplemental)
    {
        var path = result.GetValue(certificate) ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
        var keyPath = result.GetValue(key);
        var usePrompt = result.GetValue(prompt);
        var reference = result.GetValue(store);
        if (usePrompt && reference is not null) Conflict();
        var bundle = Path.GetExtension(path).Equals(".pfx", StringComparison.OrdinalIgnoreCase)
            || Path.GetExtension(path).Equals(".p12", StringComparison.OrdinalIgnoreCase);
        if (bundle ? keyPath is not null : string.IsNullOrWhiteSpace(keyPath)) Conflict();
        var source = usePrompt || reference is not null ? new PasswordSource(services, usePrompt, reference) : null;
        lifetime = bundle ? new Pkcs12CertificateOptions(path) { DecryptionPasswordSource = source, SupplementalCredentials = supplemental }
            : new X509CertificateOptions(path, keyPath ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict)) { DecryptionPasswordSource = source, SupplementalCredentials = supplemental };
        return target.WithAuthentication(ConnectionAuthentication.ClientCertificate(lifetime, result.GetValue(ca)));
    }
    public void Dispose() { lifetime?.Dispose(); lifetime = null; }
    private static void Conflict() => throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialConflict);
    private sealed class PasswordSource(IServiceProvider services, bool prompt, string? reference) : IUsernamePasswordCredentialSource
    {
        public async ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (prompt)
            {
                if (!services.GetRequiredService<ICliConsole>().IsInteractive)
                    throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.InteractionRequired);
                return new(password: await services.GetRequiredService<IHiddenPasswordPrompt>().ReadAsync(cancellationToken).ConfigureAwait(false));
            }
            try
            {
                var stored = await services.GetRequiredService<IOsCredentialStore>().ReadAsync(reference ?? throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials), cancellationToken).ConfigureAwait(false);
                if (stored.Password is null) throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
                return new(password: stored.Password);
            }
            catch (OperationCanceledException) { throw; }
            catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
        }
    }
}
