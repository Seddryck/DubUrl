using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class ConnectorCatalogPublicationTests
{
    [Test]
    public async Task Defaults_DeriveStablePublicIdentityAndExpireInSevenDays()
    {
        using var fixture = new Fixture();
        var before = DateTimeOffset.UtcNow;
        var signed = await fixture.Sign();
        using var publicOnly = RSA.Create();
        publicOnly.ImportSubjectPublicKeyInfo(fixture.Key.ExportSubjectPublicKeyInfo(), out _);
        Assert.Multiple(() =>
        {
            Assert.That(signed.KeyId, Is.EqualTo("sha256-" + Convert.ToHexString(SHA256.HashData(fixture.Key.ExportSubjectPublicKeyInfo())).ToLowerInvariant()));
            Assert.That(ConnectorCatalogSignature.GetKeyId(publicOnly), Is.EqualTo(signed.KeyId));
            Assert.That(signed.ExpiresAt, Is.InRange(before.AddDays(7), DateTimeOffset.UtcNow.AddDays(7)));
            Assert.That(signed.Revision, Is.GreaterThanOrEqualTo(before.ToUnixTimeMilliseconds()));
            Assert.That(ConnectorCatalogSignature.Read(signed.Signature).KeyId, Is.EqualTo(signed.KeyId));
        });
    }

    [Test]
    public async Task NoExpiration_VerifiesInTheFutureAndStillRejectsRollback()
    {
        using var fixture = new Fixture();
        var first = await fixture.Sign(noExpiration: true);
        var second = await fixture.Sign(noExpiration: true);
        Assert.That(ConnectorCatalogSignature.Read(first.Signature).ExpiresAt, Is.Zero);
        Assert.That(first.ExpiresAt, Is.Null);
        Assert.That(second.Revision, Is.GreaterThan(first.Revision));
        var path = Path.Combine(fixture.Root, "catalog.json");
        await File.WriteAllBytesAsync(path, fixture.Catalog);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "public.pem"), fixture.Key.ExportSubjectPublicKeyInfoPem());
        var source = new ConnectorCatalogSource
        {
            Id = "enterprise", Uri = new Uri(path), SignatureUri = new Uri(path + ".sig"), TrustMode = "signed",
            TrustedSigningKeys = [new() { KeyId = "legacy-key-name", PublicKeyPath = Path.Combine(fixture.Root, "public.pem") }],
        };
        using var http = new HttpClient();
        var loader = new TrustedConnectorCatalogs(http, Path.Combine(fixture.Root, "client-state"), new FutureTime());
        await File.WriteAllBytesAsync(path + ".sig", second.Signature);
        Assert.That((await loader.LoadAsync([source])).Entries, Has.Count.EqualTo(1));
        await File.WriteAllBytesAsync(path + ".sig", first.Signature);
        Assert.That((await loader.LoadAsync([source])).Entries, Is.Empty);
    }

    [Test]
    public async Task ExplicitExpiry_IsRespectedAndContradictoryChoicesAreRejected()
    {
        using var fixture = new Fixture();
        var expiry = DateTimeOffset.UtcNow.AddHours(2);
        var signed = await ConnectorCatalogPublication.SignAsync(fixture.Catalog, fixture.Key, "enterprise", expiresAt: expiry, publisherStatePath: fixture.State);
        Assert.That(signed.ExpiresAt, Is.EqualTo(expiry));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ConnectorCatalogPublication.SignAsync(fixture.Catalog, fixture.Key, "enterprise",
            expiresAt: expiry, noExpiration: true, publisherStatePath: fixture.State));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ConnectorCatalogPublication.SignAsync(fixture.Catalog, fixture.Key, "enterprise",
            expiresAt: DateTimeOffset.UtcNow.AddDays(-1), publisherStatePath: fixture.State));
    }

    [Test]
    public async Task PersistedState_SurvivesKeyRotationAndSourceIdCasing()
    {
        using var fixture = new Fixture();
        var first = await fixture.Sign();
        using var nextKey = RSA.Create(2048);
        var next = await ConnectorCatalogPublication.SignAsync(fixture.Catalog, nextKey, "ENTERPRISE", publisherStatePath: fixture.State);
        Assert.That(next.Revision, Is.GreaterThan(first.Revision));
        Assert.That(next.KeyId, Is.Not.EqualTo(first.KeyId));
        Assert.That(Directory.GetFiles(fixture.State, "*.json"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Counter_IsAtomicAcrossConcurrentReservationsAndClockRollback()
    {
        using var fixture = new Fixture();
        Assert.That(await ConnectorCatalogPublication.ReserveAsync(fixture.State, "enterprise", 1000, default), Is.EqualTo(1000));
        Assert.That(await ConnectorCatalogPublication.ReserveAsync(fixture.State, "enterprise", 10, default), Is.EqualTo(1001));
        var values = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => ConnectorCatalogPublication.ReserveAsync(fixture.State, "enterprise", 5, default)));
        Assert.That(values.Order(), Is.EqualTo(Enumerable.Range(1002, 16).Select(value => (long)value)));
        Assert.That(await ConnectorCatalogPublication.ReserveAsync(fixture.State, "other", 1000, default), Is.EqualTo(1000));
    }

    [TestCase("corrupt")]
    [TestCase("client-state")]
    [TestCase("exhausted")]
    public async Task InvalidState_FailsClosedWithoutResetting(string fault)
    {
        using var fixture = new Fixture();
        await ConnectorCatalogPublication.ReserveAsync(fixture.State, "enterprise", 1000, default);
        var path = Directory.GetFiles(fixture.State, "*.json").Single();
        var contents = fault switch
        {
            "client-state" => "{\"SchemaVersion\":1,\"Revision\":1000,\"PayloadSha256\":\"" + new string('0', 64) + "\"}",
            "exhausted" => "{\"SchemaVersion\":1,\"Purpose\":\"catalog-publisher\",\"Revision\":9223372036854775807}",
            _ => "invalid",
        };
        await File.WriteAllTextAsync(path, contents);
        await Assert.CatchAsync<Exception>(async () => await fixture.Sign());
        Assert.That(File.ReadAllText(path), Is.EqualTo(contents));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "duburl-publisher-" + Guid.NewGuid().ToString("N"));
        public string State => Path.Combine(Root, "state");
        public RSA Key { get; } = RSA.Create(2048);
        public byte[] Catalog { get; } = JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2, [new()
        {
            Provider = "fixture", Version = "1.0.0", DubUrlApiRange = "*", Assembly = "fixture.dll", Driver = "Fixture",
            ImplementationAssemblies = ["fixture.dll"], DriverAssembly = "fixture.dll", Databases = [new("Fixture", ["fixture"])],
            TargetFrameworks = ["net10.0"], RuntimeIdentifiers = [], DownloadUri = new Uri("https://example.test/fixture.zip"), Sha256 = new string('0', 64),
        }]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
        public Task<SignedConnectorCatalog> Sign(bool noExpiration = false)
            => ConnectorCatalogPublication.SignAsync(Catalog, Key, "enterprise", noExpiration: noExpiration, publisherStatePath: State);
        public void Dispose() { Key.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class FutureTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2100, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
