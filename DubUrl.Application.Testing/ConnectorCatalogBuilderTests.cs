using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DubUrl.Core.ProviderPacks;
using NUnit.Framework;

namespace DubUrl.Application.Testing;

public class ConnectorCatalogBuilderTests
{
    [TestCase("sha256")]
    [TestCase("unsigned")]
    [TestCase("signed")]
    public async Task Build_ProducesConsumableSnapshotAndOfflineInstallation(string mode)
    {
        using var fixture = new Fixture();
        fixture.Pack("name with space#é%.ZIP");
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "unrelated.pem"), "ignored input");
        Directory.CreateDirectory(Path.Combine(fixture.Input, "nested"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Input, "nested", "broken.zip"), "ignored subfolder");
        var original = await File.ReadAllBytesAsync(Path.Combine(fixture.Input, "name with space#é%.ZIP"));
        var result = await new ConnectorCatalogBuilder().CreateAsync(fixture.Options(mode));
        using var configuration = JsonDocument.Parse(await File.ReadAllBytesAsync(Path.Combine(fixture.Output, "source-settings.json")));
        var source = configuration.RootElement.GetProperty("providerPacks").GetProperty("sources")[0]
            .Deserialize<ConnectorCatalogSource>(Fixture.Json)!;
        using var http = new HttpClient();
        var catalog = await new TrustedConnectorCatalogs(http, Path.Combine(fixture.Root, "revisions")).LoadAsync([source]);
        var entry = catalog.Entries.Single();
        var installed = await new ConnectorService(new ProviderPackStore(Path.Combine(fixture.Root, "store")), catalog, http)
            .InstallOfflineAsync("alias", Path.Combine(fixture.Output, "archives", "name with space#é%.ZIP"));
        Assert.Multiple(() =>
        {
            Assert.That(result.ConnectorCount, Is.EqualTo(1));
            Assert.That(result.CatalogSha256, Is.EqualTo(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(fixture.Output, "connector-catalog.json"))))).IgnoreCase);
            Assert.That(entry.DownloadUri!.AbsoluteUri, Is.EqualTo("https://example.test/packs/name%20with%20space%23%C3%A9%25.ZIP"));
            Assert.That(entry.Sha256, Is.EqualTo(Convert.ToHexString(SHA256.HashData(original))).IgnoreCase);
            Assert.That(installed.Active, Is.True);
            Assert.That(installed.CatalogProvenance!.TrustMode, Is.EqualTo(mode));
            Assert.That(installed.CatalogProvenance.SourceId, Is.EqualTo("enterprise"));
            Assert.That(File.ReadAllBytes(Path.Combine(fixture.Input, "name with space#é%.ZIP")), Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(Path.Combine(fixture.Output, "archives", "name with space#é%.ZIP")), Is.EqualTo(original));
            Assert.That(File.Exists(Path.Combine(fixture.Output, "archives", "unrelated.pem")), Is.False);
            Assert.That(Directory.GetFiles(fixture.Root, "*.staging"), Is.Empty);
            Assert.That(File.Exists(Path.Combine(fixture.Output, "connector-catalog.json.sha256")), Is.EqualTo(mode == "sha256"));
            Assert.That(File.Exists(Path.Combine(fixture.Output, "connector-catalog.json.sig")), Is.EqualTo(mode == "signed"));
            Assert.That(configuration.RootElement.ToString(), Does.Not.Contain("forged").And.Not.Contain("PRIVATE KEY"));
        });
    }

    [Test]
    public async Task RepeatedBuilds_AreDeterministicAndUseManifestIdentity()
    {
        using var fixture = new Fixture();
        fixture.Pack("z.zip", id: "a");
        fixture.Pack("a.zip", id: "z", aliases: ["other"]);
        var first = await new ConnectorCatalogBuilder().CreateAsync(fixture.Options());
        var bytes = await File.ReadAllBytesAsync(Path.Combine(fixture.Output, "connector-catalog.json"));
        var other = Path.Combine(fixture.Root, "second");
        var second = await new ConnectorCatalogBuilder().CreateAsync(fixture.Options(output: other));
        Assert.That(File.ReadAllBytes(Path.Combine(other, "connector-catalog.json")), Is.EqualTo(bytes));
        Assert.That(second.CatalogSha256, Is.EqualTo(first.CatalogSha256));
        using var stream = new MemoryStream(bytes);
        Assert.That(ProviderPackCatalog.Read(stream).Entries.Select(entry => entry.Provider), Is.EqualTo(new[] { "a", "z" }));
    }

    [TestCase("sha256")]
    [TestCase("unsigned")]
    [TestCase("signed")]
    public async Task HostingTemplate_ReferencesThePublishedCatalog(string mode)
    {
        using var fixture = new Fixture();
        fixture.Pack("pack.zip");
        await new ConnectorCatalogBuilder().CreateAsync(fixture.Options(mode, catalogUri: new Uri("https://example.test/catalog/current.json")));
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Output, "source-settings.json")));
        var source = json.RootElement.GetProperty("providerPacks").GetProperty("sources")[0].Deserialize<ConnectorCatalogSource>(Fixture.Json)!;
        Assert.That(source.Uri, Is.EqualTo(new Uri("https://example.test/catalog/current.json")));
        Assert.That(source.SignatureUri, Is.EqualTo(mode == "signed" ? new Uri("https://example.test/catalog/current.json.sig") : null));
        if (mode == "signed")
        {
            Assert.That(source.MinimumRevision, Is.EqualTo(1));
            Assert.That(File.ReadAllText(source.TrustedSigningKeys.Single().PublicKeyPath), Does.Contain("BEGIN PUBLIC KEY").And.Not.Contain("PRIVATE KEY"));
        }
    }

    [Test]
    public async Task PublicOnlySigningKey_FailsWithoutPublishingOrDowngrading()
    {
        using var fixture = new Fixture();
        fixture.Pack("pack.zip");
        using var publicKey = RSA.Create();
        publicKey.ImportSubjectPublicKeyInfo(fixture.Key.ExportSubjectPublicKeyInfo(), out _);
        var failure = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorCatalogBuilder().CreateAsync(new()
        {
            InputDirectory = fixture.Input, OutputDirectory = fixture.Output, ArchiveBaseUri = new Uri("https://example.test/packs/"),
            TrustMode = "signed", SigningKey = publicKey, KeyId = "key-1", Revision = 1, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        }));
        Assert.That(failure!.Code, Is.EqualTo("catalog-signing-failed"));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
        Assert.That(Directory.GetDirectories(fixture.Root, "*.staging"), Is.Empty);
    }

    [TestCase("zip")]
    [TestCase("manifest-missing")]
    [TestCase("manifest-invalid")]
    [TestCase("metadata")]
    [TestCase("module")]
    [TestCase("traversal")]
    [TestCase("backslash-traversal")]
    [TestCase("absolute")]
    [TestCase("duplicate-path")]
    [TestCase("duplicate-manifest")]
    [TestCase("symlink")]
    [TestCase("file-parent")]
    [TestCase("reserved-path")]
    public async Task InvalidArchives_FailWithoutPublishingPartialOutput(string fault)
    {
        using var fixture = new Fixture();
        fixture.Pack("a-valid.zip", id: "healthy");
        fixture.Pack("z-invalid.zip", fault: fault);
        var failure = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorCatalogBuilder().CreateAsync(fixture.Options()));
        Assert.Multiple(() =>
        {
            Assert.That(failure!.Code, Is.EqualTo("catalog-archive-invalid"));
            Assert.That(failure.ToString(), Does.Not.Contain("token=secret").And.Not.Contain("password"));
            Assert.That(Directory.Exists(fixture.Output), Is.False);
            Assert.That(Directory.GetDirectories(fixture.Root, "*.staging"), Is.Empty);
        });
    }

    [TestCase("id")]
    [TestCase("version")]
    [TestCase("alias")]
    [TestCase("id-alias")]
    public async Task AmbiguousArchives_AreRejected(string collision)
    {
        using var fixture = new Fixture();
        fixture.Pack("one.zip", id: "first", aliases: ["common"]);
        fixture.Pack("two.zip", id: collision is "id" or "version" ? "FIRST" : "second",
            version: collision == "version" ? "2.0.0" : "1.0.0", aliases: [collision == "id-alias" ? "first" : collision == "alias" ? "COMMON" : "unique"]);
        var failure = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorCatalogBuilder().CreateAsync(fixture.Options()));
        Assert.That(failure!.Code, Is.EqualTo("catalog-ambiguous"));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
    }

    [Test]
    public async Task ExistingOutputAndMissingOrEmptyInput_ProduceStableFailures()
    {
        using var fixture = new Fixture();
        var builder = new ConnectorCatalogBuilder();
        var empty = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await builder.CreateAsync(fixture.Options()));
        Assert.That(empty!.Code, Is.EqualTo("catalog-folder-empty"));
        Directory.Delete(fixture.Input);
        var missing = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await builder.CreateAsync(fixture.Options()));
        Assert.That(missing!.Code, Is.EqualTo("catalog-folder-missing"));
        Directory.CreateDirectory(fixture.Input);
        fixture.Pack("pack.zip");
        await builder.CreateAsync(fixture.Options());
        var bytes = File.ReadAllBytes(Path.Combine(fixture.Output, "connector-catalog.json"));
        var exists = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await builder.CreateAsync(fixture.Options()));
        Assert.That(exists!.Code, Is.EqualTo("catalog-output-exists"));
        Assert.That(File.ReadAllBytes(Path.Combine(fixture.Output, "connector-catalog.json")), Is.EqualTo(bytes));
    }

    [TestCase("http://example.test/packs/")]
    [TestCase("https://user:password@example.test/packs/")]
    [TestCase("https://example.test/packs/?token=secret")]
    [TestCase("https://example.test/packs/#secret")]
    public async Task UnsafeHostingUrls_AreRejectedWithoutEchoingValues(string url)
    {
        using var fixture = new Fixture();
        fixture.Pack("pack.zip");
        var failure = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorCatalogBuilder().CreateAsync(new()
        { InputDirectory = fixture.Input, OutputDirectory = fixture.Output, ArchiveBaseUri = new Uri(url) }));
        Assert.That(failure!.Code, Is.EqualTo("catalog-url-invalid"));
        Assert.That(failure.ToString(), Does.Not.Contain("password").And.Not.Contain("secret"));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
    }

    [TestCase("unknown")]
    [TestCase("signed")]
    public async Task InvalidTrustOrAbsentSigningInputs_NeverDowngrade(string mode)
    {
        using var fixture = new Fixture();
        fixture.Pack("pack.zip");
        var failure = await Assert.ThrowsAsync<ConnectorOperationException>(async () => await new ConnectorCatalogBuilder().CreateAsync(new()
        { InputDirectory = fixture.Input, OutputDirectory = fixture.Output, ArchiveBaseUri = new Uri("https://example.test/"), TrustMode = mode }));
        Assert.That(failure!.Code, Is.EqualTo("catalog-options-invalid"));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
    }

    [Test]
    public async Task Cancellation_LeavesNoSnapshotOrStagingDirectory()
    {
        using var fixture = new Fixture();
        fixture.Pack("pack.zip");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Assert.ThrowsAsync<ConnectorOperationCanceledException>(async () => await new ConnectorCatalogBuilder().CreateAsync(fixture.Options(), cancellation.Token));
        Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
        Assert.That(Directory.GetDirectories(fixture.Root, "*.staging"), Is.Empty);
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "duburl-catalog-build-" + Guid.NewGuid().ToString("N"));
        public string Input => Path.Combine(Root, "input");
        public string Output => Path.Combine(Root, "output");
        public RSA Key { get; } = RSA.Create(2048);
        public Fixture() => Directory.CreateDirectory(Input);
        public ConnectorCatalogBuildOptions Options(string mode = "sha256", string? output = null, Uri? catalogUri = null) => new()
        {
            InputDirectory = Input, OutputDirectory = output ?? Output, ArchiveBaseUri = new Uri("https://example.test/packs/"), TrustMode = mode,
            CatalogUri = catalogUri,
            SigningKey = mode == "signed" ? Key : null, KeyId = mode == "signed" ? "key-1" : null,
            Revision = mode == "signed" ? 1 : null, ExpiresAt = mode == "signed" ? DateTimeOffset.UtcNow.AddHours(1) : null,
        };
        public void Pack(string name, string id = "fixture", string version = "1.0.0", string[]? aliases = null, string? fault = null)
        {
            var path = Path.Combine(Input, name);
            if (fault == "zip") { File.WriteAllText(path, "not a ZIP"); return; }
            using var file = File.Create(path);
            using var zip = new ZipArchive(file, ZipArchiveMode.Create);
            var definition = new ConnectorDefinition
            {
                Provider = id, Version = version, Aliases = aliases ?? ["alias"], DubUrlApiRange = "[0.0.0,999.0.0)",
                Assembly = "module.dll", Driver = "Fixture", DriverAssembly = "module.dll", ImplementationAssemblies = ["module.dll"],
                Databases = fault == "metadata" ? [] : [new("Fixture", ["fixture"])], TargetFrameworks = ["net8.0", "net9.0", "net10.0"], RuntimeIdentifiers = [],
                DownloadUri = new Uri("https://user:password@example.test/?token=secret"), CatalogProvenance = new("forged", "forged", "0", null, null),
            };
            if (fault != "manifest-missing")
            {
                using var stream = zip.CreateEntry(ConnectorDefinition.FileName).Open();
                if (fault == "manifest-invalid") stream.Write("invalid"u8);
                else JsonSerializer.Serialize(stream, definition, Json);
            }
            if (fault != "module")
            {
                using var module = zip.CreateEntry("module.dll").Open();
                module.Write("fake module; never loaded"u8);
            }
            var extra = fault switch
            {
                "traversal" => "../escape", "backslash-traversal" => "..\\escape", "absolute" => "/escape",
                "duplicate-path" => "MODULE.dll", "duplicate-manifest" => "provider-pack.json", "symlink" => "link",
                "file-parent" => "module.dll/child", "reserved-path" => "CON.txt", _ => null,
            };
            if (extra is not null)
            {
                var entry = zip.CreateEntry(extra);
                if (fault == "symlink") entry.ExternalAttributes = 0xA000 << 16;
                using var stream = entry.Open();
                stream.Write(Encoding.UTF8.GetBytes("unsafe"));
            }
        }
        public void Dispose() { Key.Dispose(); Directory.Delete(Root, true); }
    }
}
