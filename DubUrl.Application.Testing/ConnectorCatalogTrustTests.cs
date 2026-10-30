using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class ConnectorCatalogTrustTests
{
    [Test]
    public async Task IndependentPublisher_CanUseTheDocumentedV1Framing()
    {
        using var fixture = new Fixture();
        var expiry = fixture.Now.AddHours(1).ToUnixTimeSeconds();
        var header = Encoding.UTF8.GetBytes("DubUrl.ConnectorCatalog/v1\nenterprise\nkey-1\n7\n"
            + expiry.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\n");
        var payload = header.Concat(fixture.Catalog).ToArray();
        var signature = fixture.Key.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        fixture.Signature = JsonSerializer.SerializeToUtf8Bytes(new ConnectorCatalogSignature(1, "enterprise", "key-1", 7, expiry,
            Convert.ToBase64String(signature)), Fixture.Json);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Has.Count.EqualTo(1));
    }

    [TestCase("unsigned")]
    [TestCase("signed")]
    public async Task CatalogUpdates_DoNotRequireClientConfigurationChanges(string mode)
    {
        using var fixture = new Fixture();
        var source = fixture.Source(mode);
        var first = await fixture.Load(source);
        fixture.Publish("second", 2);
        var second = await fixture.Load(source);
        Assert.Multiple(() =>
        {
            Assert.That(first.Entries.Single().Provider, Is.EqualTo("first"));
            Assert.That(second.Entries.Single().Provider, Is.EqualTo("second"));
            Assert.That(second.Entries.Single().CatalogProvenance!.TrustMode, Is.EqualTo(mode));
            Assert.That(second.Entries.Single().CatalogProvenance!.CatalogSha256, Is.Not.EqualTo(first.Entries.Single().CatalogProvenance!.CatalogSha256));
            if (mode == "signed")
            {
                Assert.That(second.Entries.Single().CatalogProvenance!.SigningKeyId, Is.EqualTo("key-1"));
                Assert.That(second.Entries.Single().CatalogProvenance!.SigningKeySha256, Has.Length.EqualTo(64));
                Assert.That(second.Entries.Single().CatalogProvenance!.CatalogRevision, Is.EqualTo(2));
            }
            else Assert.That(Directory.Exists(fixture.StatePath), Is.False);
        });
    }

    [TestCase("sha256")]
    [TestCase("typo")]
    [TestCase("")]
    public async Task AbsentHash_OnlyExplicitUnsignedModeCanLoad(string mode)
    {
        using var fixture = new Fixture();
        var result = await fixture.Load(new ConnectorCatalogSource { Id = "enterprise", Uri = fixture.CatalogUri, TrustMode = mode });
        Assert.That(result.Entries, Is.Empty);
        Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("connector-source-unavailable"));
    }

    [TestCase("unsigned")]
    [TestCase("signed")]
    public async Task EveryTrustMode_EnforcesCatalogSchemaAndArchiveDigests(string mode)
    {
        using var fixture = new Fixture();
        var invalidDigest = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Fixture.Document("bad")).Replace(new string('0', 64), "bad-digest"));
        foreach (var bytes in new[] { "not json"u8.ToArray(), invalidDigest })
        {
            fixture.Catalog = bytes;
            fixture.Signature = ConnectorCatalogSignature.Sign(bytes, fixture.Key, "enterprise", "key-1", 1, fixture.Now.AddHours(1));
            var result = await fixture.Load(fixture.Source(mode));
            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.Diagnostics, Has.Count.EqualTo(1));
        }
        Assert.That(Directory.Exists(fixture.StatePath), Is.False);
    }

    [TestCase("unsigned", "https")]
    [TestCase("signed", "https")]
    [TestCase("unsigned", "http")]
    [TestCase("signed", "http")]
    public async Task ConflictingTrustSettingsAndHttp_AreRejectedBeforeRequests(string mode, string scheme)
    {
        using var fixture = new Fixture();
        var source = new ConnectorCatalogSource
        {
            Id = "enterprise", TrustMode = mode, Uri = new Uri(scheme + "://secret.test/catalog"),
            Sha256 = scheme == "https" ? Convert.ToHexString(SHA256.HashData(fixture.Catalog)) : null,
            SignatureUri = mode == "signed" ? fixture.SignatureUri : null,
            TrustedSigningKeys = mode == "signed" ? [fixture.TrustedKey] : [],
        };
        Assert.That((await fixture.Load(source)).Entries, Is.Empty);
        Assert.That(fixture.Handler.Authorizations, Is.Empty);
    }

    [TestCase("bytes")]
    [TestCase("whitespace")]
    [TestCase("signature")]
    [TestCase("missing-signature")]
    [TestCase("malformed-signature")]
    [TestCase("version")]
    [TestCase("revision")]
    [TestCase("expiry")]
    [TestCase("source")]
    [TestCase("key-id")]
    [TestCase("untrusted-key")]
    [TestCase("public-key-file")]
    [TestCase("private-key-file")]
    public async Task SignedFailures_NeverDowngradeOrExposeAuthentication(string failure)
    {
        using var fixture = new Fixture();
        var envelope = JsonSerializer.Deserialize<ConnectorCatalogSignature>(fixture.Signature, Fixture.Json)!;
        switch (failure)
        {
            case "bytes": fixture.Catalog = Fixture.Document("tampered"); break;
            case "whitespace": fixture.Catalog = fixture.Catalog.Concat(new byte[] { 32 }).ToArray(); break;
            case "signature": envelope = envelope with { Signature = Convert.ToBase64String(new byte[256]) }; break;
            case "missing-signature": fixture.SignatureMissing = true; break;
            case "malformed-signature": envelope = envelope with { Signature = "https://user:password@secret.test?token=secret" }; break;
            case "version": envelope = envelope with { SchemaVersion = 99 }; break;
            case "revision": envelope = envelope with { Revision = 999 }; break;
            case "expiry": envelope = envelope with { ExpiresAt = envelope.ExpiresAt + 3600 }; break;
            case "source": envelope = envelope with { SourceId = "another-source" }; break;
            case "key-id": envelope = envelope with { KeyId = "unknown" }; break;
            case "untrusted-key":
                using (var other = RSA.Create(2048)) await File.WriteAllTextAsync(fixture.PublicKeyPath, other.ExportSubjectPublicKeyInfoPem());
                break;
            case "public-key-file": File.Delete(fixture.PublicKeyPath); break;
            // Private key fixtures are generated in memory and never written to the repository or disk.
            case "private-key-file": await File.WriteAllTextAsync(fixture.PublicKeyPath, "-----BEGIN PRIVATE KEY-----\ninvalid\n-----END PRIVATE KEY-----"); break;
        }
        fixture.Signature = JsonSerializer.SerializeToUtf8Bytes(envelope, Fixture.Json);
        var healthy = new ProviderPackCatalog([Fixture.Entry("healthy")]);
        var result = await fixture.Load(fixture.Source("signed"), healthy);
        Assert.Multiple(() =>
        {
            Assert.That(result.Entries.Single().Provider, Is.EqualTo("healthy"));
            Assert.That(result.Diagnostics.Single().Code, Is.EqualTo("connector-source-unavailable"));
            Assert.That(JsonSerializer.Serialize(result.Diagnostics), Does.Not.Contain("password").And.Not.Contain("secret.test").And.Not.Contain("token="));
            Assert.That(Directory.Exists(fixture.StatePath), Is.False);
        });
    }

    [Test]
    public async Task ExpiryAndMinimumRevision_RejectStaleCatalogEvenOnFreshClient()
    {
        using var fixture = new Fixture();
        fixture.Signature = ConnectorCatalogSignature.Sign(fixture.Catalog, fixture.Key, "enterprise", "key-1", 1, fixture.Now);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Is.Empty);
        fixture.Publish("first", 1);
        Assert.That((await fixture.Load(fixture.Source("signed", minimumRevision: 2))).Entries, Is.Empty);
        Assert.That(Directory.Exists(fixture.StatePath), Is.False);
    }

    [Test]
    public async Task RevisionState_SurvivesNewLoader_RejectsRollbackAndSameRevisionReplacement()
    {
        using var fixture = new Fixture();
        var oldCatalog = fixture.Catalog;
        var oldSignature = fixture.Signature;
        fixture.Publish("newer", 2);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries.Single().Provider, Is.EqualTo("newer"));
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Has.Count.EqualTo(1));
        fixture.Catalog = oldCatalog;
        fixture.Signature = oldSignature;
        // A rolled-back source must be removed before collision checking against healthy sources.
        var healthy = new ProviderPackCatalog([Fixture.Entry("first")]);
        var rolledBack = await fixture.Load(fixture.Source("signed"), healthy);
        Assert.That(rolledBack.Entries.Single().CatalogProvenance!.SourceId, Is.EqualTo("official"));
        Assert.That(rolledBack.Diagnostics, Has.Count.EqualTo(1));
        fixture.Publish("replacement", 2);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Is.Empty);
        fixture.Publish("next", 3);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries.Single().Provider, Is.EqualTo("next"));
    }

    [Test]
    public async Task KeyRotation_UsesConfiguredKeysAndRetainsSourceHighWaterMark()
    {
        using var fixture = new Fixture();
        using var nextKey = RSA.Create(2048);
        var nextPath = Path.Combine(fixture.Root, "next-public.pem");
        await File.WriteAllTextAsync(nextPath, nextKey.ExportSubjectPublicKeyInfoPem());
        var next = new ConnectorCatalogSigningKey { KeyId = "key-2", PublicKeyPath = nextPath };
        var both = fixture.Source("signed", keys: [fixture.TrustedKey, next]);
        Assert.That((await fixture.Load(both)).Entries, Has.Count.EqualTo(1));
        fixture.Signature = ConnectorCatalogSignature.Sign(fixture.Catalog, nextKey, "enterprise", "key-2", 2, fixture.Now.AddHours(1));
        var rotated = await fixture.Load(both);
        Assert.That(rotated.Entries.Single().CatalogProvenance!.SigningKeyId, Is.EqualTo("key-2"));
        var retired = fixture.Source("signed", keys: [next]);
        Assert.That((await fixture.Load(retired)).Entries, Has.Count.EqualTo(1));
        fixture.Publish("first", 3);
        Assert.That((await fixture.Load(retired)).Entries, Is.Empty);
        fixture.Signature = ConnectorCatalogSignature.Sign(fixture.Catalog, nextKey, "enterprise", "key-2", 1, fixture.Now.AddHours(1));
        Assert.That((await fixture.Load(retired)).Entries, Is.Empty);
    }

    [Test]
    public async Task InvalidCatalogOrCollision_DoesNotAdvanceRevisionState()
    {
        using var fixture = new Fixture();
        fixture.Catalog = "not json"u8.ToArray();
        fixture.Signature = ConnectorCatalogSignature.Sign(fixture.Catalog, fixture.Key, "enterprise", "key-1", 99, fixture.Now.AddHours(1));
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Is.Empty);
        fixture.Publish("first", 99);
        await Assert.ThrowsAsync<ConnectorOperationException>(async () => await fixture.Load(fixture.Source("signed"),
            new ProviderPackCatalog([Fixture.Entry("first")])));
        fixture.Publish("first", 1);
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task CorruptOrUnwritableState_FailsClosedWithoutHidingOtherSources()
    {
        using var fixture = new Fixture();
        await fixture.Load(fixture.Source("signed"));
        var path = Directory.GetFiles(fixture.StatePath, "*.json").Single();
        await File.WriteAllTextAsync(path, "invalid state");
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Is.Empty);
        Directory.Delete(fixture.StatePath, true);
        await File.WriteAllTextAsync(fixture.StatePath, "occupied");
        Assert.That((await fixture.Load(fixture.Source("signed"))).Entries, Is.Empty);
    }

    [Test]
    public async Task ConcurrentLoads_PersistTheHighestAcceptedRevision()
    {
        using var fixture = new Fixture();
        var store = new ConnectorCatalogRevisionStore(fixture.StatePath);
        var attempts = Enumerable.Range(1, 12).Select(async revision =>
        {
            try { await store.AcceptAsync("enterprise", revision, new string('0', 64), default); }
            catch (InvalidDataException) { /* A newer concurrent revision may already have won. */ }
        });
        await Task.WhenAll(attempts);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await store.AcceptAsync("enterprise", 11, new string('0', 64), default));
        await store.AcceptAsync("enterprise", 12, new string('0', 64), default);
    }

    [TestCase("unsigned")]
    [TestCase("signed")]
    public async Task LocalFiles_UseTheSameTrustChecks(string mode)
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Root, "catalog.json");
        await File.WriteAllBytesAsync(path, fixture.Catalog);
        await File.WriteAllBytesAsync(path + ".sig", fixture.Signature);
        var source = new ConnectorCatalogSource
        {
            Id = "enterprise", Uri = new Uri(path), TrustMode = mode,
            SignatureUri = mode == "signed" ? new Uri(path + ".sig") : null,
            TrustedSigningKeys = mode == "signed" ? [fixture.TrustedKey] : [],
        };
        Assert.That((await fixture.Load(source)).Entries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SignatureCredentials_AreScopedToConfiguredOriginAndNotDefaultHeaders()
    {
        using var fixture = new Fixture();
        var variable = "DUBURL_TEST_SIGNING_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "catalog-test-token");
        try
        {
            var source = new ConnectorCatalogSource
            {
                Id = "enterprise", Uri = fixture.CatalogUri, TrustMode = "signed", SignatureUri = fixture.SignatureUri,
                TrustedSigningKeys = [fixture.TrustedKey], BearerTokenEnvironmentVariable = variable,
            };
            Assert.That((await fixture.Load(source)).Entries, Has.Count.EqualTo(1));
            Assert.That(fixture.Handler.Authorizations, Is.EqualTo(new[] { "Bearer catalog-test-token", "Bearer catalog-test-token" }));
            Assert.That(fixture.Http.DefaultRequestHeaders.Authorization, Is.Null);
            fixture.Handler.Authorizations.Clear();
            var invalid = new ConnectorCatalogSource
            {
                Id = "enterprise", Uri = fixture.CatalogUri, TrustMode = "signed", SignatureUri = new Uri("https://other.test/signature"),
                TrustedSigningKeys = [fixture.TrustedKey], BearerTokenEnvironmentVariable = variable,
            };
            Assert.That((await fixture.Load(invalid)).Entries, Is.Empty);
            Assert.That(fixture.Handler.Authorizations, Is.Empty);
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "duburl-trust-" + Guid.NewGuid().ToString("N"));
        public string StatePath => Path.Combine(Root, "revisions");
        public string PublicKeyPath => Path.Combine(Root, "public.pem");
        public DateTimeOffset Now { get; } = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public Uri CatalogUri { get; } = new("https://secret.test/catalog?token=secret");
        public Uri SignatureUri { get; } = new("https://secret.test/signature?token=secret");
        public RSA Key { get; } = RSA.Create(2048);
        public byte[] Catalog { get; set; } = [];
        public byte[] Signature { get; set; } = [];
        public bool SignatureMissing { get; set; }
        public CatalogHandler Handler { get; }
        public HttpClient Http { get; }
        public ConnectorCatalogSigningKey TrustedKey => new() { KeyId = "key-1", PublicKeyPath = PublicKeyPath };

        public Fixture()
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(PublicKeyPath, Key.ExportSubjectPublicKeyInfoPem());
            Handler = new CatalogHandler(this);
            Http = new HttpClient(Handler);
            Publish("first", 1);
        }

        public void Publish(string id, long revision)
        {
            Catalog = Document(id);
            Signature = ConnectorCatalogSignature.Sign(Catalog, Key, "enterprise", "key-1", revision, Now.AddHours(1));
        }

        public ConnectorCatalogSource Source(string mode, long minimumRevision = 1, IReadOnlyList<ConnectorCatalogSigningKey>? keys = null)
            => new()
            {
                Id = "enterprise", Uri = CatalogUri, TrustMode = mode, MinimumRevision = minimumRevision,
                SignatureUri = mode == "signed" ? SignatureUri : null,
                TrustedSigningKeys = mode == "signed" ? keys ?? [TrustedKey] : [],
            };

        public Task<ProviderPackCatalog> Load(ConnectorCatalogSource source, ProviderPackCatalog? official = null)
            => new TrustedConnectorCatalogs(Http, StatePath, new FixedTime(Now)).LoadAsync([source], official);

        public static byte[] Document(string id)
            => JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2, [Entry(id)]), Json);

        public static ConnectorDefinition Entry(string id) => new()
        {
            Provider = id, Aliases = [], Version = "1.0.0", DownloadUri = new Uri("https://secret.test/archive?token=secret"),
            Sha256 = new string('0', 64), DubUrlApiRange = "*", Assembly = "fixture.dll", Driver = "Fixture",
            ImplementationAssemblies = ["fixture.dll"], Databases = [new("Fixture", ["fixture"])],
            TargetFrameworks = ["net10.0"], RuntimeIdentifiers = [],
        };

        public void Dispose()
        {
            Http.Dispose();
            Key.Dispose();
            Directory.Delete(Root, true);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class CatalogHandler(Fixture fixture) : HttpMessageHandler
    {
        public List<string?> Authorizations { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorizations.Add(request.Headers.Authorization?.ToString());
            var signature = request.RequestUri == fixture.SignatureUri;
            return Task.FromResult(new HttpResponseMessage(signature && fixture.SignatureMissing ? HttpStatusCode.NotFound : HttpStatusCode.OK)
            { Content = new ByteArrayContent(signature ? fixture.Signature : fixture.Catalog) });
        }
    }
}
