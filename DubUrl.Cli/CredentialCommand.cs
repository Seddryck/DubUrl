using System.CommandLine;
using DubUrl.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class CredentialCommand
{
    public static Command Create(IServiceProvider services, CancellationToken token)
    {
        var group = new Command("credential", "Manage named credentials in the current user's native OS store.");
        foreach (var verb in new[] { "add", "remove", "exists" })
        {
            var command = new Command(verb, verb switch
            {
                "add" => "Save a username/password pair; prompt with input hidden unless --password-stdin is selected.",
                "remove" => "Remove a named credential; an absent entry is a successful outcome.",
                _ => "Check presence without reading the password: 0 present, 1 absent, 10 store error."
            });
            var name = new Argument<string>("name") { Description = "Case-insensitive name: 1–128 ASCII letters, digits, dots, hyphens or underscores; start with a letter or digit." };
            var username = new Option<string>("--username") { Required = true, Description = "Username stored with the credential." };
            var stdin = new Option<bool>("--password-stdin") { Description = "Read password to EOF; remove exactly one final LF or CRLF, preserving all other characters." };
            var replace = new Option<bool>("--replace") { Description = "Replace an existing credential, including its username." };
            command.Arguments.Add(name);
            if (verb == "add")
            {
                command.Options.Add(username);
                command.Options.Add(stdin);
                command.Options.Add(replace);
            }
            command.SetAction(result => ExecuteAsync(services, verb, result.GetValue(name) ?? string.Empty,
                verb == "add" ? result.GetValue(username) : null,
                verb == "add" && result.GetValue(stdin), verb == "add" && result.GetValue(replace), token));
            group.Subcommands.Add(command);
        }
        return group;
    }

    private static async Task<int> ExecuteAsync(IServiceProvider services, string verb, string name, string? username, bool stdin, bool replace, CancellationToken token)
    {
        var console = services.GetRequiredService<ICliConsole>();
        try
        {
            name = CredentialName.Normalize(name);
            token.ThrowIfCancellationRequested();
            var store = services.GetRequiredService<INamedCredentialStore>();
            if (verb == "exists")
                return await store.ExistsAsync(name, token).ConfigureAwait(false) ? CliExitCodes.Success : CliExitCodes.CredentialAbsent;
            if (verb == "remove")
                await store.RemoveAsync(name, token).ConfigureAwait(false);
            else
            {
                var exit = await AddAsync(services, store, name, username, stdin, replace, token).ConfigureAwait(false);
                if (exit != CliExitCodes.Success) return exit;
            }
            await console.Out.WriteLineAsync(verb == "add" ? "Credential saved." : "Credential removed or already absent.").ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            await console.Error.WriteLineAsync("Operation cancelled.").ConfigureAwait(false);
            return CliExitCodes.Cancelled;
        }
        catch (ArgumentException)
        {
            // Never forward arbitrary adapter exception messages: they can contain secrets.
            await console.Error.WriteLineAsync("Invalid credential input. Use a portable name, a nonempty username and a password of at most 1280 UTF-16 code units.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        catch (CredentialStoreException exception)
        {
            await console.Error.WriteLineAsync(new CredentialStoreException(exception.AlreadyExists).Message).ConfigureAwait(false);
            return CliExitCodes.CredentialStoreFailed;
        }
        catch
        {
            await console.Error.WriteLineAsync(new CredentialStoreException().Message).ConfigureAwait(false);
            return CliExitCodes.CredentialStoreFailed;
        }
    }

    private static async Task<int> AddAsync(IServiceProvider services, INamedCredentialStore store, string name,
        string? username, bool stdin, bool replace, CancellationToken token)
    {
        var console = services.GetRequiredService<ICliConsole>();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 512 || username.Any(char.IsControl))
            throw new ArgumentException("A nonempty username of up to 512 characters without control characters is required.");
        if (!stdin && !console.IsInteractive)
        {
            await console.Error.WriteLineAsync("Password input requires an interactive terminal. Use --password-stdin for automation.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        if (!replace && await store.ExistsAsync(name, token).ConfigureAwait(false))
            throw new CredentialStoreException(alreadyExists: true);
        var password = stdin ? await ReadPasswordAsync(console.In, token).ConfigureAwait(false)
            : await services.GetRequiredService<IHiddenPasswordPrompt>().ReadAsync(token).ConfigureAwait(false);
        var credentials = new UsernamePasswordCredentials(username, password);
        CredentialInput.ValidateCredentials(credentials);
        token.ThrowIfCancellationRequested();
        await store.AddAsync(name, credentials, replace, token).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static async Task<string> ReadPasswordAsync(TextReader input, CancellationToken token)
    {
        // Bound input before allocation; two extra characters allow a final CRLF.
        var buffer = new char[1283];
        try
        {
            var length = 0;
            while (length < buffer.Length)
            {
                var count = await input.ReadAsync(buffer.AsMemory(length), token).ConfigureAwait(false);
                if (count == 0) break;
                length += count;
            }
            if (length > 0 && buffer[length - 1] == '\n')
            {
                length--;
                if (length > 0 && buffer[length - 1] == '\r') length--;
            }
            if (length > 1280) throw new ArgumentException("Password exceeds portable limit.");
            return new string(buffer, 0, length);
        }
        finally { Array.Clear(buffer); }
    }
}
