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
            command.SetAction(result => ExecuteAsync(services, verb, result.GetValue(name)!,
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
                if (string.IsNullOrWhiteSpace(username) || username.Length > 513 || username.Any(char.IsControl))
                    throw new ArgumentException("A nonempty username of up to 513 characters without control characters is required.");
                if (!stdin && !console.IsInteractive)
                {
                    console.Error.WriteLine("Password input requires an interactive terminal. Use --password-stdin for automation.");
                    return CliExitCodes.InvalidInput;
                }
                if (!replace && await store.ExistsAsync(name, token).ConfigureAwait(false))
                    throw new CredentialStoreException(alreadyExists: true);
                var password = stdin ? await ReadPasswordAsync(console.In, token).ConfigureAwait(false)
                    : await services.GetRequiredService<IHiddenPasswordPrompt>().ReadAsync(token).ConfigureAwait(false);
                var credentials = new UsernamePasswordCredentials(username, password);
                NamedCredentialStore.ValidateCredentials(credentials);
                token.ThrowIfCancellationRequested();
                await store.AddAsync(name, credentials, replace, token).ConfigureAwait(false);
            }
            console.Out.WriteLine(verb == "add" ? "Credential saved." : "Credential removed or already absent.");
            return CliExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            console.Error.WriteLine("Operation cancelled.");
            return CliExitCodes.Cancelled;
        }
        catch (ArgumentException)
        {
            // Never forward arbitrary adapter exception messages: they can contain secrets.
            console.Error.WriteLine("Invalid credential input. Use a portable name, a nonempty username and a password of at most 1280 UTF-16 code units.");
            return CliExitCodes.InvalidInput;
        }
        catch (CredentialStoreException exception)
        {
            console.Error.WriteLine(new CredentialStoreException(exception.AlreadyExists).Message);
            return CliExitCodes.CredentialStoreFailed;
        }
        catch
        {
            console.Error.WriteLine(new CredentialStoreException().Message);
            return CliExitCodes.CredentialStoreFailed;
        }
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
