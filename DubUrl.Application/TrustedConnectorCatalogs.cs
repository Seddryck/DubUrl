using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using DubUrl.Core.ProviderPacks;

namespace DubUrl.Application;

/// <summary>Administrator-approved catalog pinned to an independently obtained digest.</summary>
public sealed class ConnectorCatalogSource
{
    public required string Id { get; init; }
    public required Uri Uri { get; init; }
    public required string Sha256 { get; init; }
    public bool Enabled { get; init; } = true;
    /// <summary>Optional environment variable holding a bearer token for the catalog request only.</summary>
    public string? BearerTokenEnvironmentVariable { get; init; }
    public override string ToString() => "Connector catalog source (location and authentication redacted)";
}

/// <summary>Loads independent sources, rejects ambiguity, and never retains transport exception details.</summary>
public sealed class TrustedConnectorCatalogs(HttpClient httpClient)
{
    public async Task<ProviderPackCatalog> LoadAsync(
        IEnumerable<ConnectorCatalogSource> sources,
        ProviderPackCatalog? official = null,
        CancellationToken cancellationToken = default)
    {
        var ordered = sources.OrderBy(source => source.Id, StringComparer.Ordinal).ToArray();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "official" };
        foreach (var source in ordered)
            if (!IsSafeName(source.Id) || !ids.Add(source.Id))
                throw new ConnectorOperationException("connector-source-invalid", "Catalog source IDs must be unique simple names; 'official' is reserved.");

        var entries = new List<ConnectorDefinition>();
        var diagnostics = new List<ProviderPackDiagnostic>();
        if (official is not null)
        {
            diagnostics.AddRange(official.Diagnostics);
            entries.AddRange(official.Entries.Select(entry => entry with
            {
                CatalogProvenance = new("official", entry.Provider, entry.Version, null, entry.Sha256),
            }));
        }
        foreach (var source in ordered.Where(source => source.Enabled))
        {
            try
            {
                if (!source.Uri.IsAbsoluteUri || (!source.Uri.IsFile && source.Uri.Scheme != Uri.UriSchemeHttps)
                    || source.Uri.UserInfo.Length != 0 || source.Sha256.Length != 64 || !source.Sha256.All(Uri.IsHexDigit))
                    throw new InvalidDataException();
                byte[] bytes;
                if (source.Uri.IsFile)
                    bytes = await File.ReadAllBytesAsync(source.Uri.LocalPath, cancellationToken).ConfigureAwait(false);
                else
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, source.Uri);
                    if (source.BearerTokenEnvironmentVariable is not null)
                    {
                        var token = Environment.GetEnvironmentVariable(source.BearerTokenEnvironmentVariable);
                        if (string.IsNullOrWhiteSpace(token)) throw new InvalidDataException();
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    }
                    using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                }
                if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(source.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException();
                using var stream = new MemoryStream(bytes);
                // Ignore provenance claimed by the document: only the configured source establishes trust.
                var catalog = ProviderPackCatalog.Read(stream);
                if (catalog.Entries.Any(entry => !IsSafeName(entry.Provider) || entry.Aliases is null || entry.Aliases.Any(alias => !IsSafeName(alias))))
                    throw new InvalidDataException();
                entries.AddRange(catalog.Entries.Select(entry => entry with
                {
                    CatalogProvenance = new(source.Id, entry.Provider, entry.Version, source.Sha256.ToLowerInvariant(), entry.Sha256),
                }));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException
                or OperationCanceledException or JsonException or ArgumentException or InvalidOperationException or FormatException)
            {
                diagnostics.Add(new("connector-source-unavailable", $"Catalog source '{source.Id}' could not be loaded or verified."));
            }
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OrderBy(entry => entry.CatalogProvenance!.SourceId, StringComparer.Ordinal)
                     .ThenBy(entry => entry.Provider, StringComparer.Ordinal))
            foreach (var name in new[] { entry.Provider }.Concat(entry.Aliases))
            {
                var source = entry.CatalogProvenance!.SourceId;
                if (!names.TryAdd(name, source))
                    throw new ConnectorOperationException("connector-catalog-collision",
                        $"Connector ID or alias '{name}' is ambiguous between catalog sources '{names[name]}' and '{source}'.");
            }
        return new ProviderPackCatalog(entries.OrderBy(entry => entry.Provider, StringComparer.Ordinal)) { Diagnostics = diagnostics };
    }

    private static bool IsSafeName(string? value)
        => value is not null && Regex.IsMatch(value, @"\A[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\z");
}
