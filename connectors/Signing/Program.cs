using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;

if (args.Length != 7)
{
    Console.Error.WriteLine("Usage: ConnectorCatalog.Sign <catalog> <new-signature> <encrypted-private-pem> <source-id> <key-id> <revision> <expiry-UTC>");
    return 2;
}

try
{
    var password = Environment.GetEnvironmentVariable("DUBURL_CATALOG_SIGNING_PASSWORD");
    if (string.IsNullOrEmpty(password)
        || !long.TryParse(args[5], NumberStyles.None, CultureInfo.InvariantCulture, out var revision)
        || !DateTimeOffset.TryParse(args[6], CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var expiry)
        || expiry <= DateTimeOffset.UtcNow)
        throw new InvalidDataException();
    var bytes = await File.ReadAllBytesAsync(args[0]);
    using (var stream = new MemoryStream(bytes)) ProviderPackCatalog.Read(stream);
    using var key = RSA.Create();
    key.ImportFromEncryptedPem(await File.ReadAllTextAsync(args[2]), password);
    var signature = ConnectorCatalogSignature.Sign(bytes, key, args[3], args[4], revision, expiry);
    // CreateNew prevents accidentally replacing the catalog, private key, or an existing published signature.
    await using var output = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None);
    await output.WriteAsync(signature);
    output.Flush(flushToDisk: true);
    Console.WriteLine("Catalog signature created.");
    return 0;
}
catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
    or CryptographicException or JsonException or ArgumentException or FormatException)
{
    Console.Error.WriteLine("Catalog signing failed. Check the input, encrypted key, password environment variable, and new output destination.");
    return 1;
}
