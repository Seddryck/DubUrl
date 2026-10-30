using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class TrustedConnectorCatalogsTests
{
    [Test]
    public async Task MultipleSources_MergeDeterministicallyAndKeepProvenance()
    {
        var bytes = Document(Entry("private", "corp"));
        using var http = new HttpClient(new Handler(bytes));
        var loader = new TrustedConnectorCatalogs(http);
        var sources = new[] { Source("z", bytes), Source("a", Document(Entry("other", "second"))) };
        // Local catalogs exercise the same pinned-byte verification as HTTPS.
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, Document(Entry("other", "second")));
            sources[1] = new ConnectorCatalogSource { Id = "a", Uri = new Uri(path), Sha256 = sources[1].Sha256 };
            var first = await loader.LoadAsync(sources, new ProviderPackCatalog([Entry("official-pack", "public")]));
            var second = await loader.LoadAsync(sources.Reverse(), new ProviderPackCatalog([Entry("official-pack", "public")]));
            Assert.Multiple(() =>
            {
                Assert.That(first.Entries.Select(entry => entry.Provider), Is.EqualTo(second.Entries.Select(entry => entry.Provider)));
                Assert.That(first.Entries.Select(entry => entry.Provider), Is.EqualTo(new[] { "official-pack", "other", "private" }));
                Assert.That(first.TryGet("CORP", out var entry), Is.True);
                Assert.That(entry!.CatalogProvenance, Is.EqualTo(new ConnectorCatalogProvenance("z", "private", "1.0.0", sources[0].Sha256!.ToLowerInvariant(), entry.Sha256) { TrustMode = "sha256" }));
            });
        }
        finally { File.Delete(path); }
    }

    [TestCase("same", "first", "same", "second")]
    [TestCase("one", "same", "two", "SAME")]
    [TestCase("one", "two", "two", "other")]
    public async Task Collisions_NameBothSources(string firstId, string firstAlias, string secondId, string secondAlias)
    {
        var bytes = Document(Entry(secondId, secondAlias));
        using var http = new HttpClient(new Handler(bytes));
        var loader = new TrustedConnectorCatalogs(http);
        var official = new ProviderPackCatalog([Entry(firstId, firstAlias)]);
        var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await loader.LoadAsync([Source("enterprise", bytes)], official));
        Assert.Multiple(() =>
        {
            Assert.That(exception!.Code, Is.EqualTo("connector-catalog-collision"));
            Assert.That(exception.Message, Does.Contain("official").And.Contain("enterprise"));
        });
    }

    [TestCase("transport")]
    [TestCase("digest")]
    [TestCase("json")]
    [TestCase("metadata")]
    public async Task FailedSource_DoesNotHideHealthySourcesOrLeakAuthenticatedLocations(string failure)
    {
        var bytes = failure == "json" ? "invalid"u8.ToArray() : Document(Entry("private", "corp") with
        {
            DownloadUri = new Uri(failure == "metadata" ? "http://unsafe.test/pack" : "https://private.test/pack?token=secret"),
        });
        using var http = new HttpClient(new Handler(bytes, fail: failure == "transport"));
        var source = new ConnectorCatalogSource
        {
            Id = "enterprise", Uri = new Uri("https://private.test/catalog?token=secret"),
            Sha256 = failure == "digest" ? new string('0', 64) : Hash(bytes),
        };
        var catalog = await new TrustedConnectorCatalogs(http).LoadAsync([source], new ProviderPackCatalog([Entry("public", "pub")]));
        var output = JsonSerializer.Serialize(catalog.Diagnostics);
        Assert.Multiple(() =>
        {
            Assert.That(catalog.Entries.Single().Provider, Is.EqualTo("public"));
            Assert.That(catalog.Diagnostics.Single().Code, Is.EqualTo("connector-source-unavailable"));
            Assert.That(output, Does.Contain("enterprise").And.Not.Contain("secret").And.Not.Contain("private.test"));
            Assert.That(source.ToString(), Does.Not.Contain("secret"));
        });
    }

    [Test]
    public async Task NamedSourceCollision_ReportsTheSameDiagnosticRegardlessOfConfigurationOrder()
    {
        var bytes = Document(Entry("private", "corp"));
        using var http = new HttpClient(new Handler(bytes));
        var loader = new TrustedConnectorCatalogs(http);
        var sources = new[] { Source("z", bytes), Source("a", bytes) };
        var first = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await loader.LoadAsync(sources));
        var second = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await loader.LoadAsync(sources.Reverse()));
        Assert.Multiple(() =>
        {
            Assert.That(first!.Message, Is.EqualTo(second!.Message));
            Assert.That(first.Message, Does.Contain("'a'").And.Contain("'z'"));
        });
    }

    [Test]
    public async Task DisabledSource_IsNotContacted_AndNoOfficialCatalogIsRequired()
    {
        using var http = new HttpClient(new Handler([], fail: true));
        var catalog = await new TrustedConnectorCatalogs(http).LoadAsync([
            new ConnectorCatalogSource { Id = "enterprise", Uri = new Uri("https://private.test"), Sha256 = "", Enabled = false }]);
        Assert.Multiple(() =>
        {
            Assert.That(catalog.Entries, Is.Empty);
            Assert.That(catalog.Diagnostics, Is.Empty);
        });
    }

    [Test]
    public async Task BearerAuthentication_IsScopedToTheCatalogRequest()
    {
        var variable = "DUBURL_TEST_CATALOG_" + Guid.NewGuid().ToString("N");
        Environment.SetEnvironmentVariable(variable, "test-token");
        var bytes = Document(Entry("private", "corp"));
        var handler = new Handler(bytes);
        using var http = new HttpClient(handler);
        try
        {
            var catalog = await new TrustedConnectorCatalogs(http).LoadAsync([
                new ConnectorCatalogSource { Id = "enterprise", Uri = new Uri("https://private.test"), Sha256 = Hash(bytes), BearerTokenEnvironmentVariable = variable }]);
            Assert.Multiple(() =>
            {
                Assert.That(handler.Authorization, Is.EqualTo("Bearer test-token"));
                Assert.That(http.DefaultRequestHeaders.Authorization, Is.Null);
                Assert.That(JsonSerializer.Serialize(catalog.Entries.Single().CatalogProvenance), Does.Not.Contain("test-token").And.Not.Contain("private.test"));
            });
        }
        finally { Environment.SetEnvironmentVariable(variable, null); }
    }

    [TestCase("official", "official")]
    [TestCase("duplicate", "DUPLICATE")]
    [TestCase("https://secret", "other")]
    public async Task InvalidSourceIds_AreRejectedWithoutEchoingValues(string first, string second)
    {
        using var http = new HttpClient(new Handler([]));
        var exception = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new TrustedConnectorCatalogs(http)
            .LoadAsync([Source(first, []), Source(second, [])]));
        Assert.That(exception!.ToString(), Does.Not.Contain("https://secret"));
    }

    private static ConnectorCatalogSource Source(string id, byte[] bytes)
        => new() { Id = id, Uri = new Uri("https://private.test/catalog"), Sha256 = Hash(bytes) };

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] Document(ConnectorDefinition entry)
        => JsonSerializer.SerializeToUtf8Bytes(new ProviderPackCatalogDocument(2, [entry]), new JsonSerializerOptions(JsonSerializerDefaults.Web));
    private static ConnectorDefinition Entry(string id, string alias) => new()
    {
        Provider = id, Aliases = [alias], Version = "1.0.0", DownloadUri = new Uri("https://private.test/pack"),
        Sha256 = new string('0', 64), DubUrlApiRange = "*", Assembly = "fixture.dll", Driver = "Fixture",
        ImplementationAssemblies = ["fixture.dll"], Databases = [new("Fixture", ["fixture"])],
        TargetFrameworks = ["net10.0"], RuntimeIdentifiers = [],
    };

    private sealed class Handler(byte[] bytes, bool fail = false) : HttpMessageHandler
    {
        public string? Authorization { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            if (fail) throw new HttpRequestException("https://user:password@private.test/?token=secret");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
        }
    }
}
