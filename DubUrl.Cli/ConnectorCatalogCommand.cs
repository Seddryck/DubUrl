using System.CommandLine;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Application;
using Microsoft.Extensions.DependencyInjection;

namespace DubUrl.Cli;

internal static class ConnectorCatalogCommand
{
    public static Command Create(IServiceProvider services, CancellationToken cancellationToken)
    {
        var folder = new Argument<string>("folder") { Description = "Folder of provider-pack ZIPs. Subfolders and unrelated files are ignored." };
        var output = new Option<string>("--output") { Required = true, Description = "New directory for catalog artifacts and copied archives." };
        var archiveBase = new Option<string>("--archive-base-uri") { Required = true, Description = "Clean HTTPS location where the copied archives will be hosted." };
        var mode = new Option<string?>("--trust-mode") { Description = "sha256 (default), unsigned, or signed." };
        var sourceId = new Option<string?>("--source-id") { Description = "Stable catalog source ID (default enterprise)." };
        var catalogUri = new Option<string?>("--catalog-uri") { Description = "Published catalog HTTPS or file URI. Defaults to the generated local catalog." };
        var signingKey = new Option<string?>("--signing-key") { Description = "Encrypted PKCS#8 private PEM path, or '-' for redirected stdin; signed mode only." };
        var expires = new Option<string?>("--expires-at") { Description = "ISO 8601 expiry or 'none' (default: no expiration)." };
        var publisherState = new Option<string?>("--publisher-state") { Description = "Persistent publisher revision directory. Defaults to DubUrl/catalog-publication-revisions in local application data." };
        var passwordEnv = new Option<string?>("--signing-password-env") { Description = "Password environment variable (default DUBURL_CATALOG_SIGNING_PASSWORD). Never pass the password itself." };
        var json = new Option<bool>("--json") { Description = "Write stable result metadata with relative artifact references." };
        var create = new Command("create", "Prepare a catalog from top-level archives without uploading, installing, or loading provider code.");
        create.Arguments.Add(folder);
        foreach (var option in new Option[] { output, archiveBase, mode, sourceId, catalogUri, signingKey, expires, publisherState, passwordEnv, json })
            create.Options.Add(option);
        create.SetAction(async result =>
        {
            var console = services.GetRequiredService<ICliConsole>();
            var asJson = result.GetValue(json);
            RSA? key = null;
            try
            {
                var trustMode = result.GetValue(mode) ?? "sha256";
                if (!Uri.TryCreate(result.GetValue(archiveBase), UriKind.Absolute, out var baseUri)
                    || (result.GetValue(catalogUri) is string location && !Uri.TryCreate(location, UriKind.Absolute, out _)))
                    throw new ConnectorOperationException("catalog-url-invalid", "Specify an absolute hosting URI without credentials, query, or fragment.");
                DateTimeOffset? expiry = null;
                var noExpiration = string.Equals(result.GetValue(expires), "none", StringComparison.OrdinalIgnoreCase);
                if (!noExpiration && result.GetValue(expires) is string expiresText)
                {
                    if (!DateTimeOffset.TryParse(expiresText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)) throw InvalidSigning();
                    expiry = parsed;
                }
                if (trustMode == "signed")
                {
                    var path = result.GetValue(signingKey);
                    var password = Environment.GetEnvironmentVariable(result.GetValue(passwordEnv) ?? "DUBURL_CATALOG_SIGNING_PASSWORD");
                    if (string.IsNullOrWhiteSpace(path) || string.IsNullOrEmpty(password)) throw InvalidSigning();
                    if (path == "-" && !console.IsInputRedirected)
                        throw new ConnectorOperationException("catalog-options-invalid", "Use redirected stdin with '--signing-key -'. The key password must come from its environment variable.");
                    key = RSA.Create();
                    var pem = path == "-"
                        ? await console.In.ReadToEndAsync(cancellationToken).ConfigureAwait(false)
                        : await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
                    key.ImportFromEncryptedPem(pem, password);
                }
                else if (result.GetValue(signingKey) is not null || result.GetValue(passwordEnv) is not null)
                    throw InvalidSigning();
                var built = await new ConnectorCatalogBuilder().CreateAsync(new ConnectorCatalogBuildOptions
                {
                    InputDirectory = result.GetValue(folder)!, OutputDirectory = result.GetValue(output)!, ArchiveBaseUri = baseUri,
                    CatalogUri = result.GetValue(catalogUri) is string uri ? new Uri(uri) : null,
                    TrustMode = trustMode, SourceId = result.GetValue(sourceId) ?? "enterprise", SigningKey = key,
                    ExpiresAt = expiry, NoExpiration = noExpiration, PublisherStatePath = result.GetValue(publisherState),
                }, cancellationToken).ConfigureAwait(false);
                if (asJson) console.Out.WriteLine(JsonSerializer.Serialize(new { command = "catalog create", success = true, result = built }, CliJson.Options));
                else console.Out.WriteLine($"Prepared {built.ConnectorCount} connectors in {built.TrustMode} mode. Catalog SHA-256: {built.CatalogSha256}. Artifacts: {string.Join(", ", built.Artifacts)}");
                return CliExitCodes.Success;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { return Report("connector-cancelled", "Catalog generation was cancelled.", CliExitCodes.Cancelled); }
            catch (ConnectorOperationException exception)
            { return Report(exception.Code, exception.Message, CliExitCodes.ProviderOperationFailed); }
            catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException or FormatException)
            { return Report("catalog-signing-failed", "Signing inputs could not be read or used. Check the encrypted key and password environment variable.", CliExitCodes.ProviderOperationFailed); }
            finally { key?.Dispose(); }

            int Report(string code, string message, int exitCode)
            {
                if (asJson) console.Out.WriteLine(JsonSerializer.Serialize(new { command = "catalog create", success = false, diagnostics = new[] { new { code, message } } }, CliJson.Options));
                else console.Error.WriteLine($"{code}: {message}");
                return exitCode;
            }
        });
        var command = new Command("catalog", "Prepare connector catalogs for administrator-managed publication.");
        command.Subcommands.Add(create);
        return command;
    }

    private static ConnectorOperationException InvalidSigning()
        => new("catalog-options-invalid", "Signed mode requires an encrypted private key and password environment variable. Optional expiry must be a future timestamp or 'none'.");
}
