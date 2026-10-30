using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;

if (args.Length is < 4 or > 6)
{
    Console.Error.WriteLine("Usage: ConnectorCatalog.Sign <catalog> <new-signature> <encrypted-private-pem-or-> <source-id> [expiry-UTC|none|default] [publisher-state-directory]");
    return 2;
}

try
{
    var password = Environment.GetEnvironmentVariable("DUBURL_CATALOG_SIGNING_PASSWORD");
    if (string.IsNullOrEmpty(password) || File.Exists(args[1]) || Directory.Exists(args[1])) throw new InvalidDataException();
    DateTimeOffset? expiry = null;
    var noExpiration = args.Length >= 5 && args[4].Equals("none", StringComparison.OrdinalIgnoreCase);
    if (args.Length >= 5 && !noExpiration && !args[4].Equals("default", StringComparison.OrdinalIgnoreCase))
    {
        if (!DateTimeOffset.TryParse(args[4], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            || parsed <= DateTimeOffset.UtcNow) throw new InvalidDataException();
        expiry = parsed;
    }
    var bytes = await File.ReadAllBytesAsync(args[0]);
    using (var stream = new MemoryStream(bytes)) ProviderPackCatalog.Read(stream);
    using var key = RSA.Create();
    if (args[2] == "-" && !Console.IsInputRedirected) throw new InvalidDataException();
    var pem = args[2] == "-" ? await Console.In.ReadToEndAsync() : await File.ReadAllTextAsync(args[2]);
    key.ImportFromEncryptedPem(pem, password);
    var publication = await ConnectorCatalogPublication.SignAsync(bytes, key, args[3], expiry, noExpiration,
        args.Length == 6 ? args[5] : null);
    // CreateNew prevents accidentally replacing the catalog, private key, or an existing published signature.
    await using var output = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await output.WriteAsync(publication.Signature);
    output.Flush(flushToDisk: true);
    Console.WriteLine($"Catalog signature created. Key ID: {publication.KeyId}. Revision: {publication.Revision}. Expiry: {publication.ExpiresAt?.ToString("o") ?? "none"}.");
    return 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
    or CryptographicException or JsonException or ArgumentException or FormatException)
{
    Console.Error.WriteLine("Catalog signing failed. Check the input, encrypted key, password environment variable, and new output destination.");
    return 1;
}
