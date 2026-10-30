using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Application;
using DubUrl.Core.ProviderPacks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ConnectorCatalogCommandTests
{
    [TestCase("sha256", false)]
    [TestCase("sha256", true)]
    [TestCase("unsigned", false)]
    [TestCase("unsigned", true)]
    [TestCase("signed", false)]
    [TestCase("signed", true)]
    public async Task Create_AllModes_EmitConsumableCatalogAndSafeResults(string mode, bool json)
    {
        using var fixture = new Fixture();
        var arguments = fixture.Arguments(mode).ToList();
        if (json) arguments.Add("--json");
        var console = new ConsoleCapture();
        await using var services = CliComposition.CreateForCommand(arguments.ToArray(), console, fixture.Configuration);
        Assert.That(await CliHost.InvokeAsync(arguments.ToArray(), services), Is.Zero, console.Errors);
        var output = console.Output + console.Errors;
        Assert.That(output, Does.Not.Contain(fixture.Password).And.Not.Contain(fixture.PrivatePath).And.Not.Contain("PRIVATE KEY").And.Not.Contain("https://"));
        if (json)
        {
            using var document = JsonDocument.Parse(console.Output);
            Assert.That(document.RootElement.GetProperty("success").GetBoolean(), Is.True);
            Assert.That(document.RootElement.GetProperty("command").GetString(), Is.EqualTo("catalog create"));
            Assert.That(document.RootElement.GetProperty("result").GetProperty("trustMode").GetString(), Is.EqualTo(mode));
            Assert.That(document.RootElement.GetProperty("result").GetProperty("catalogSha256").GetString(), Has.Length.EqualTo(64));
        }
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(fixture.Output, "source-settings.json"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ProviderPacks:OfficialEnabled"] = "false", ["ProviderPacks:StorePath"] = Path.Combine(fixture.Root, "consumer-store"),
            }).Build();
        var consumer = new ConsoleCapture();
        await using var client = CliComposition.CreateDefault(consumer, configuration);
        Assert.That(await CliHost.InvokeAsync(["connector", "list", "--json"], client), Is.Zero);
        using var listed = JsonDocument.Parse(consumer.Output);
        var connector = listed.RootElement.GetProperty("connectors")[0];
        Assert.That(connector.GetProperty("id").GetString(), Is.EqualTo("fixture"));
        Assert.That(connector.GetProperty("catalogProvenance").GetProperty("trustMode").GetString(), Is.EqualTo(mode));
        Assert.That(await CliHost.InvokeAsync(["connector", "install", "alias", "--archive", Path.Combine(fixture.Output, "archives", "pack.zip"), "--json"], client), Is.Zero);
    }

    [TestCase("missing-password")]
    [TestCase("bad-password")]
    [TestCase("expired")]
    [TestCase("bad-expiry")]
    [TestCase("bad-key")]
    [TestCase("url")]
    [TestCase("mode")]
    [TestCase("state")]
    public async Task InvalidInputs_FailWithRedactedJsonAndNoPublishedOutput(string failure)
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("signed").Concat(new[] { "--json" }).ToArray();
        if (failure == "missing-password") Environment.SetEnvironmentVariable(fixture.Variable, null);
        if (failure == "bad-password") Environment.SetEnvironmentVariable(fixture.Variable, "invalid-password");
        if (failure is "expired" or "bad-expiry") args = args.Concat(new[] { "--expires-at", failure == "expired" ? "2000-01-01T00:00:00Z" : "https://user:password@secret.test/?token=secret" }).ToArray();
        if (failure == "bad-key") await File.WriteAllTextAsync(fixture.PrivatePath, "invalid key");
        if (failure == "url") args[Array.IndexOf(args, "--archive-base-uri") + 1] = "https://user:password@secret.test/?token=secret";
        if (failure == "mode") args[Array.IndexOf(args, "--trust-mode") + 1] = "unknown";
        if (failure == "state") await File.WriteAllTextAsync(Path.Combine(fixture.Root, "publisher-state"), "occupied");
        var console = new ConsoleCapture();
        await using var services = CliComposition.CreateForCommand(args, console, fixture.Configuration);
        Assert.That(await CliHost.InvokeAsync(args, services), Is.EqualTo(CliExitCodes.ProviderOperationFailed));
        using var output = JsonDocument.Parse(console.Output);
        Assert.That(output.RootElement.GetProperty("success").GetBoolean(), Is.False);
        Assert.That(output.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Does.StartWith("catalog-"));
        Assert.That(console.Output + console.Errors, Does.Not.Contain(fixture.Password).And.Not.Contain(fixture.PrivatePath)
            .And.Not.Contain("secret.test").And.Not.Contain("token=secret").And.Not.Contain("invalid-password"));
        Assert.That(Directory.Exists(fixture.Output), Is.False);
        Assert.That(Directory.GetDirectories(fixture.Root, "*.staging"), Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ParserErrors_NeverEchoRejectedArguments(bool json)
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("unsigned").Concat(new[] { "--unsupported", "https://user:password@secret.test/?token=secret" }).ToList();
        if (json) args.Add("--json");
        var console = new ConsoleCapture();
        await using var services = CliComposition.CreateForCommand(args.ToArray(), console, fixture.Configuration);
        Assert.That(await CliHost.InvokeAsync(args.ToArray(), services), Is.EqualTo(CliExitCodes.InvalidInput));
        Assert.That(console.Output + console.Errors, Does.Not.Contain("password").And.Not.Contain("secret.test").And.Not.Contain("token=secret"));
        if (json)
        {
            using var document = JsonDocument.Parse(console.Output);
            Assert.That(document.RootElement.GetProperty("diagnostics")[0].GetProperty("code").GetString(), Is.EqualTo("catalog-arguments-invalid"));
        }
    }

    [Test]
    public async Task OmittedTrustMode_DefaultsToSha256()
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("sha256").ToList();
        var index = args.IndexOf("--trust-mode");
        args.RemoveRange(index, 2);
        args.Add("--json");
        var console = new ConsoleCapture();
        await using var services = CliComposition.CreateForCommand(args.ToArray(), console, fixture.Configuration);
        Assert.That(await CliHost.InvokeAsync(args.ToArray(), services), Is.Zero);
        using var document = JsonDocument.Parse(console.Output);
        Assert.That(document.RootElement.GetProperty("result").GetProperty("trustMode").GetString(), Is.EqualTo("sha256"));
    }

    [Test]
    public async Task PublisherStartup_DoesNotReadClientConfigurationOrInitializeProviders()
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("unsigned").Concat(new[] { "--json" }).ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ProviderPacks:StorePath"] = "https://user:password@secret.test/?token=secret",
            ["ProviderPacks:CatalogPath"] = "invalid-path",
            ["ProviderPacks:OfficialEnabled"] = "not a boolean",
        }).Build();
        var console = new ConsoleCapture();
        await using var services = CliComposition.CreateForCommand(args, console, configuration);
        Assert.That(services.GetService<IConnectorService>(), Is.Null);
        Assert.That(services.GetService<ProviderPackStore>(), Is.Null);
        Assert.That(await CliHost.InvokeAsync(args, services), Is.Zero);
        Assert.That(console.Output + console.Errors, Does.Not.Contain("secret").And.Not.Contain("password"));
    }

    [TestCase("default")]
    [TestCase("none")]
    [TestCase("custom")]
    public async Task SignedDefaults_UseDerivedIdentityAutomaticRevisionsAndChosenExpiry(string expiryChoice)
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("signed").Concat(new[] { "--json" }).ToList();
        var customExpiry = DateTimeOffset.UtcNow.AddHours(2);
        if (expiryChoice != "default") args.AddRange(["--expires-at", expiryChoice == "none" ? "none" : customExpiry.ToString("o")]);
        var console = new ConsoleCapture();
        await using (var services = CliComposition.CreateForCommand(args.ToArray(), console))
            Assert.That(await CliHost.InvokeAsync(args.ToArray(), services), Is.Zero, console.Errors);
        var signature = JsonSerializer.Deserialize<ConnectorCatalogSignature>(File.ReadAllBytes(Path.Combine(fixture.Output, "connector-catalog.json.sig")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        using var key = RSA.Create();
        key.ImportFromPem(File.ReadAllText(Path.Combine(fixture.Output, "catalog-public.pem")));
        Assert.That(signature.KeyId, Is.EqualTo(ConnectorCatalogSignature.GetKeyId(key)));
        if (expiryChoice == "custom") Assert.That(signature.ExpiresAt, Is.EqualTo(customExpiry.ToUnixTimeSeconds()));
        else Assert.That(signature.ExpiresAt, Is.Zero);
        using var result = JsonDocument.Parse(console.Output);
        Assert.That(result.RootElement.GetProperty("result").GetProperty("catalogRevision").GetInt64(), Is.EqualTo(signature.Revision));
        var secondOutput = Path.Combine(fixture.Root, "second-output");
        args[args.IndexOf("--output") + 1] = secondOutput;
        var secondConsole = new ConsoleCapture();
        await using var secondServices = CliComposition.CreateForCommand(args.ToArray(), secondConsole);
        Assert.That(await CliHost.InvokeAsync(args.ToArray(), secondServices), Is.Zero);
        var next = JsonSerializer.Deserialize<ConnectorCatalogSignature>(File.ReadAllBytes(Path.Combine(secondOutput, "connector-catalog.json.sig")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.That(next.Revision, Is.GreaterThan(signature.Revision));
        Assert.That(next.KeyId, Is.EqualTo(signature.KeyId));
    }

    [TestCase("valid")]
    [TestCase("empty")]
    [TestCase("malformed")]
    [TestCase("wrong-password")]
    [TestCase("interactive")]
    public async Task SigningKey_FromStdin_IsValidatedWithoutEchoingKeyMaterial(string input)
    {
        using var fixture = new Fixture();
        var args = fixture.Arguments("signed").Concat(new[] { "--json" }).ToArray();
        args[Array.IndexOf(args, "--signing-key") + 1] = "-";
        var pem = input == "empty" ? "" : input == "malformed" ? "private-key-material-secret" : File.ReadAllText(fixture.PrivatePath);
        if (input == "wrong-password") Environment.SetEnvironmentVariable(fixture.Variable, "wrong-password-secret");
        var console = new ConsoleCapture { In = new StringReader(pem), IsInputRedirected = input != "interactive" };
        await using var services = CliComposition.CreateForCommand(args, console);
        Assert.That(await CliHost.InvokeAsync(args, services), Is.EqualTo(input == "valid" ? CliExitCodes.Success : CliExitCodes.ProviderOperationFailed));
        Assert.That(console.Output + console.Errors, Does.Not.Contain(fixture.Password).And.Not.Contain("BEGIN ENCRYPTED PRIVATE KEY")
            .And.Not.Contain("private-key-material-secret").And.Not.Contain("wrong-password-secret"));
        using var document = JsonDocument.Parse(console.Output);
        Assert.That(document.RootElement.GetProperty("success").GetBoolean(), Is.EqualTo(input == "valid"));
        if (input == "valid")
        {
            var sourceConfiguration = new ConfigurationBuilder().AddJsonFile(Path.Combine(fixture.Output, "source-settings.json"))
                .AddInMemoryCollection(new Dictionary<string, string?>
                { ["ProviderPacks:OfficialEnabled"] = "false", ["ProviderPacks:StorePath"] = Path.Combine(fixture.Root, "consumer-store") }).Build();
            var consumer = new ConsoleCapture();
            await using var client = CliComposition.CreateDefault(consumer, sourceConfiguration);
            Assert.That(await CliHost.InvokeAsync(["connector", "list", "--json"], client), Is.Zero);
            using var listed = JsonDocument.Parse(consumer.Output);
            Assert.That(listed.RootElement.GetProperty("connectors")[0].GetProperty("id").GetString(), Is.EqualTo("fixture"));
        }
        else Assert.That(Directory.Exists(fixture.Output), Is.False);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "duburl-catalog-cli-" + Guid.NewGuid().ToString("N"));
        public string Input => Path.Combine(Root, "input");
        public string Output => Path.Combine(Root, "output");
        public string PrivatePath => Path.Combine(Root, "private.pem");
        public string Variable { get; } = "DUBURL_TEST_PUBLISH_" + Guid.NewGuid().ToString("N");
        public string Password { get; } = Guid.NewGuid().ToString("N");
        public IConfiguration Configuration => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ProviderPacks:StorePath"] = Path.Combine(Root, "publisher-store") }).Build();
        public Fixture()
        {
            Directory.CreateDirectory(Input);
            // Ephemeral private key material is stored only in the system temp directory, never in the repository.
            using var key = RSA.Create(2048);
            File.WriteAllText(PrivatePath, key.ExportEncryptedPkcs8PrivateKeyPem(Password,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 10000)));
            Environment.SetEnvironmentVariable(Variable, Password);
            using var file = File.Create(Path.Combine(Input, "pack.zip"));
            using var zip = new ZipArchive(file, ZipArchiveMode.Create);
            using (var manifest = zip.CreateEntry(ConnectorDefinition.FileName).Open())
                JsonSerializer.Serialize(manifest, new ConnectorDefinition
                {
                    Provider = "fixture", Version = "1.0.0", Aliases = ["alias"], DubUrlApiRange = "[0.0.0,999.0.0)",
                    Assembly = "module.dll", Driver = "Fixture", DriverAssembly = "module.dll", ImplementationAssemblies = ["module.dll"],
                    Databases = [new("Fixture", ["fixture"])], TargetFrameworks = ["net8.0", "net9.0", "net10.0"], RuntimeIdentifiers = [],
                }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            using var module = zip.CreateEntry("module.dll").Open();
            module.Write("fake module; never loaded"u8);
        }
        public IEnumerable<string> Arguments(string mode)
        {
            var arguments = new List<string> { "catalog", "create", Input, "--output", Output,
                "--archive-base-uri", "https://example.test/packs/", "--trust-mode", mode };
            if (mode == "signed") arguments.AddRange(["--signing-key", PrivatePath, "--publisher-state", Path.Combine(Root, "publisher-state"),
                "--signing-password-env", Variable]);
            return arguments;
        }
        public void Dispose() { Environment.SetEnvironmentVariable(Variable, null); Directory.Delete(Root, true); }
    }

    private sealed class ConsoleCapture : ICliConsole
    {
        public TextReader In { get; init; } = TextReader.Null;
        public StringWriter OutputWriter { get; } = new();
        public StringWriter ErrorWriter { get; } = new();
        public TextWriter Out => OutputWriter;
        public TextWriter Error => ErrorWriter;
        public bool IsInputRedirected { get; init; } = true;
        public bool IsInteractive => false;
        public string Output => OutputWriter.ToString();
        public string Errors => ErrorWriter.ToString();
    }
}
