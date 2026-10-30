using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using DubUrl.Core.ProviderPacks;

namespace DubUrl.Application;

public sealed class ConnectorCatalogBuildOptions
{
    public required string InputDirectory { get; init; }
    public required string OutputDirectory { get; init; }
    public required Uri ArchiveBaseUri { get; init; }
    public Uri? CatalogUri { get; init; }
    public string SourceId { get; init; } = "enterprise";
    public string TrustMode { get; init; } = "sha256";
    /// <summary>Host-owned private key, required only in signed mode. Never serialized or copied to output.</summary>
    [JsonIgnore] public RSA? SigningKey { get; init; }
    public string? KeyId { get; init; }
    public long? Revision { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public override string ToString() => "Connector catalog build options (locations and signing material redacted)";
}

public sealed record ConnectorCatalogBuildResult(string TrustMode, int ConnectorCount, string CatalogSha256, IReadOnlyList<string> Artifacts);

/// <summary>Prepares an unpublished snapshot from top-level ZIP archives. Never loads provider code or accesses HTTP.</summary>
public sealed class ConnectorCatalogBuilder
{
    private const string CatalogName = "connector-catalog.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ConnectorCatalogBuildResult> CreateAsync(ConnectorCatalogBuildOptions options, CancellationToken cancellationToken = default)
    {
        string? staging = null;
        try
        {
            ValidateOptions(options);
            var destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.OutputDirectory));
            if (File.Exists(destination) || Directory.Exists(destination))
                throw Failure("catalog-output-exists", "Use a new output directory for each catalog snapshot.");
            if (!Directory.Exists(options.InputDirectory))
                throw Failure("catalog-folder-missing", "The provider-pack input folder does not exist.");
            var inputs = Directory.EnumerateFiles(options.InputDirectory, "*", SearchOption.TopDirectoryOnly)
                .Where(path => Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal).ToArray();
            if (inputs.Length == 0) throw Failure("catalog-folder-empty", "The input folder contains no top-level provider-pack ZIP archives.");
            var filenames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (inputs.Any(path => !filenames.Add(Path.GetFileName(path))))
                throw Failure("catalog-filename-collision", "Archive filenames must be unique, including case differences.");
            staging = destination + "." + Guid.NewGuid().ToString("N") + ".staging";
            Directory.CreateDirectory(Path.Combine(staging, "archives"));
            var entries = new List<ConnectorDefinition>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var baseUri = new Uri(options.ArchiveBaseUri.AbsoluteUri.TrimEnd('/') + "/");
            foreach (var input in inputs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((File.GetAttributes(input) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException();
                var filename = Path.GetFileName(input);
                var copyPath = Path.Combine(staging, "archives", filename);
                await using (var source = new FileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read))
                await using (var copy = new FileStream(copyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await source.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
                await using var snapshot = File.OpenRead(copyPath);
                var digest = Convert.ToHexString(await SHA256.HashDataAsync(snapshot, cancellationToken).ConfigureAwait(false)).ToLowerInvariant();
                snapshot.Position = 0;
                var definition = await ProviderPackArchive.ReadDefinitionAsync(snapshot, cancellationToken).ConfigureAwait(false);
                if (!ConnectorCatalogSignature.IsSafeName(definition.Provider) || definition.Aliases is null
                    || definition.Aliases.Any(alias => !ConnectorCatalogSignature.IsSafeName(alias))) throw new InvalidDataException();
                foreach (var name in new[] { definition.Provider }.Concat(definition.Aliases))
                    if (!names.Add(name)) throw Failure("catalog-ambiguous", "Input archives have duplicate connector IDs or aliases. Include only one version per connector.");
                entries.Add(definition with
                {
                    DownloadUri = new Uri(baseUri, Uri.EscapeDataString(filename)), Sha256 = digest, CatalogProvenance = null,
                });
            }
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2,
                entries.OrderBy(entry => entry.Provider, StringComparer.Ordinal).ToArray()), JsonOptions);
            using (var validation = new MemoryStream(bytes)) ProviderPackCatalog.Read(validation);
            var catalogDigest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            await File.WriteAllBytesAsync(Path.Combine(staging, CatalogName), bytes, cancellationToken).ConfigureAwait(false);
            var catalogUri = options.CatalogUri ?? new Uri(Path.Combine(destination, CatalogName));
            var sourceSettings = new Dictionary<string, object?>
            {
                ["Id"] = options.SourceId, ["Uri"] = catalogUri.AbsoluteUri, ["Enabled"] = true, ["TrustMode"] = options.TrustMode,
            };
            var artifacts = new List<string> { CatalogName, "archives/", "source-settings.json" };
            if (options.TrustMode == "sha256")
            {
                sourceSettings["Sha256"] = catalogDigest;
                await File.WriteAllTextAsync(Path.Combine(staging, CatalogName + ".sha256"), catalogDigest + "\n", cancellationToken).ConfigureAwait(false);
                artifacts.Add(CatalogName + ".sha256");
            }
            if (options.TrustMode == "signed")
            {
                var signature = ConnectorCatalogSignature.Sign(bytes, options.SigningKey!, options.SourceId, options.KeyId!, options.Revision!.Value, options.ExpiresAt!.Value);
                await File.WriteAllBytesAsync(Path.Combine(staging, CatalogName + ".sig"), signature, cancellationToken).ConfigureAwait(false);
                await File.WriteAllTextAsync(Path.Combine(staging, "catalog-public.pem"), options.SigningKey!.ExportSubjectPublicKeyInfoPem(), cancellationToken).ConfigureAwait(false);
                sourceSettings["SignatureUri"] = catalogUri.AbsoluteUri + ".sig";
                sourceSettings["TrustedSigningKeys"] = new[] { new { KeyId = options.KeyId!, PublicKeyPath = Path.Combine(destination, "catalog-public.pem") } };
                sourceSettings["MinimumRevision"] = options.Revision.Value;
                artifacts.AddRange([CatalogName + ".sig", "catalog-public.pem"]);
            }
            await File.WriteAllBytesAsync(Path.Combine(staging, "source-settings.json"), JsonSerializer.SerializeToUtf8Bytes(
                new { ProviderPacks = new { Sources = new[] { sourceSettings } } }, JsonOptions), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (options.TrustMode == "signed" && options.ExpiresAt <= DateTimeOffset.UtcNow)
                throw Failure("catalog-options-invalid", "The signing expiry elapsed before publication. Choose a future expiry and rebuild.");
            Directory.Move(staging, destination);
            return new(options.TrustMode, entries.Count, catalogDigest, artifacts);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw new ConnectorOperationCanceledException(cancellationToken); }
        catch (Exception exception) when (exception is InvalidDataException or JsonException or ProviderPackException)
        { throw Failure("catalog-archive-invalid", "An input ZIP has invalid metadata, unsafe paths, missing files, or unreadable contents."); }
        catch (CryptographicException) { throw Failure("catalog-signing-failed", "The catalog could not be signed with the supplied key."); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw Failure("catalog-create-failed", "The catalog snapshot could not be read or written. Check the input and new output locations."); }
        finally { if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, recursive: true); }
    }

    private static void ValidateOptions(ConnectorCatalogBuildOptions options)
    {
        if (!ConnectorCatalogSignature.IsSafeName(options.SourceId) || options.SourceId.Equals("official", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(options.InputDirectory) || string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw Failure("catalog-options-invalid", "Specify input and output folders and a unique source ID other than 'official'.");
        ValidateUri(options.ArchiveBaseUri, allowFile: false);
        if (options.CatalogUri is not null) ValidateUri(options.CatalogUri, allowFile: true);
        if (options.TrustMode == "signed")
        {
            if (options.SigningKey is null || !ConnectorCatalogSignature.IsSafeName(options.KeyId) || options.Revision is null or < 1
                || options.ExpiresAt is null || options.ExpiresAt <= DateTimeOffset.UtcNow || options.SigningKey.KeySize is < 2048 or > 8192)
                throw Failure("catalog-options-invalid", "Signed catalogs require a private signing key, key ID, positive revision, and future expiry.");
        }
        else if (options.TrustMode is not ("sha256" or "unsigned") || options.SigningKey is not null
            || options.KeyId is not null || options.Revision is not null || options.ExpiresAt is not null)
            throw Failure("catalog-options-invalid", "Select sha256, unsigned, or signed. Signing options are valid only in signed mode.");
    }

    private static void ValidateUri(Uri uri, bool allowFile)
    {
        if (!uri.IsAbsoluteUri || (uri.Scheme != Uri.UriSchemeHttps && !(allowFile && uri.IsFile))
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw Failure("catalog-url-invalid", "Use a clean absolute HTTPS hosting URL, or a file URI for a locally deployed catalog, without credentials, query, or fragment.");
    }

    private static ConnectorOperationException Failure(string code, string message) => new(code, message);
}
