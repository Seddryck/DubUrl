using DubUrl.Authentication;
using Microsoft.Extensions.Configuration;

namespace DubUrl.Extensions.Configuration;

/// <summary>Reads current string values from caller-owned configuration once per resolution.</summary>
public sealed class ConfigurationUsernamePasswordCredentialSource : ISynchronousUsernamePasswordCredentialSource
{
    private readonly IConfiguration configuration;
    private readonly string? usernameKey;
    private readonly string passwordKey;

    public ConfigurationUsernamePasswordCredentialSource(IConfiguration configuration, string? usernameKey, string passwordKey)
    {
        this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        if (usernameKey is not null) ArgumentException.ThrowIfNullOrWhiteSpace(usernameKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordKey);
        this.usernameKey = usernameKey;
        this.passwordKey = passwordKey;
    }

    public UsernamePasswordCredentials Resolve(AuthenticationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var username = usernameKey is null ? null : configuration[usernameKey];
            var password = configuration[passwordKey];
            if (password is null || (username is null && context.Username is null))
                throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.MissingCredentials);
            return new(username, password);
        }
        catch (OperationCanceledException) { throw; }
        catch (ConnectionAuthenticationException) { throw; }
        catch { throw new ConnectionAuthenticationException(AuthenticationDiagnosticCodes.CredentialResolutionFailed); }
    }

    public ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(Resolve(context));
    }
    public override string ToString() => "ConfigurationUsernamePasswordCredentialSource [REDACTED]";
}
