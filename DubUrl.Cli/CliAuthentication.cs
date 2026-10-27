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
public interface IOsCredentialStore
{
    ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default);
}

internal sealed class CliAuthentication
{
    private readonly Option<bool> prompt = new("--password-prompt") { Description = "Explicitly read a hidden password from the terminal." };
    private readonly Option<string?> reference = new("--credential-store") { Description = "Read an existing OS credential-store entry by reference." };
    private readonly Option<string?> username = new("--username") { Description = "Username for the selected external credential source." };
    public void AddTo(Command command) { command.Options.Add(prompt); command.Options.Add(reference); command.Options.Add(username); }
    public ConnectionUrl Configure(IServiceProvider services, ParseResult result, string url)
    {
        var usePrompt = result.GetValue(prompt);
        var entry = result.GetValue(reference);
        var user = result.GetValue(username);
        if (usePrompt && entry is not null || user is not null && !usePrompt && entry is null)
            throw new ConnectionAuthenticationException("CredentialConflict");
        var target = new ConnectionUrl(url, services.GetRequiredService<ISchemeRegistry>());
        if (usePrompt || entry is not null)
            target = target.WithAuthentication(ConnectionAuthentication.UsernamePassword(new Source(services, usePrompt, entry, user)));
        target.ValidateAuthentication();
        return target;
    }

    private sealed class Source(IServiceProvider services, bool prompt, string? reference, string? username) : IUsernamePasswordCredentialSource
    {
        public async ValueTask<UsernamePasswordCredentials> ResolveAsync(AuthenticationContext context, CancellationToken cancellationToken = default)
        {
            if (prompt)
            {
                if (!services.GetRequiredService<ICliConsole>().IsInteractive)
                    throw new ConnectionAuthenticationException("InteractionRequired");
                if (username is null && context.Username is null) throw new ConnectionAuthenticationException("MissingCredentials");
                var password = await services.GetRequiredService<IHiddenPasswordPrompt>().ReadAsync(cancellationToken).ConfigureAwait(false);
                return new(username, password);
            }
            var stored = await services.GetRequiredService<IOsCredentialStore>().ReadAsync(reference!, cancellationToken).ConfigureAwait(false);
            if (username is not null && stored.Username is not null && username != stored.Username)
                throw new ConnectionAuthenticationException("CredentialConflict");
            return new(username ?? stored.Username, stored.Password);
        }
    }
}

internal sealed class TerminalPasswordPrompt : IHiddenPasswordPrompt
{
    public async ValueTask<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (Console.IsInputRedirected || Console.IsOutputRedirected)
            throw new ConnectionAuthenticationException("InteractionRequired");
        Console.Error.Write("Password: ");
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
        finally { characters.Clear(); Console.Error.WriteLine(); }
    }
}
