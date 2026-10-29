using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class CredentialCommandTests
{
    [TestCase("alice", " secret \r\n", " secret ")]
    [TestCase("bob", " secret \n", " secret ")]
    [TestCase("bob", " secret ", " secret ")]
    [TestCase("bob", "a\n\n", "a\n")]
    [TestCase("bob", "a\r", "a\r")]
    [TestCase("bob", "a\0b\n", "a\0b")]
    [TestCase("bob", "", "")]
    public async Task StdinPreservesPasswordExceptOneFinalLineTerminator(string username, string input, string expected)
    {
        var store = new MemoryStore();
        var console = new TestConsole(input);
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        var exit = await CliHost.InvokeAsync(["credential", "add", "Orders-Dev", "--username", username, "--password-stdin"], services);
        Assert.That(exit, Is.Zero);
        Assert.That(store.Value?.Username, Is.EqualTo(username));
        Assert.That(store.Value?.Password == expected, Is.True);
        Assert.That(store.LastName, Is.EqualTo("orders-dev"));
        Assert.That(store.Reads, Is.Zero);
    }

    [Test]
    public async Task DuplicateDoesNotPromptOrOverwriteAndReplaceChangesUsername()
    {
        var store = new MemoryStore { Value = new("alice", "sentinel") };
        var prompt = new TestPrompt();
        var console = new TestConsole(interactive: true);
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "bob"], services), Is.EqualTo(10));
        Assert.That(prompt.Calls, Is.Zero);
        Assert.That(store.Writes, Is.Zero);
        Assert.That(console.Errors.ToString(), Does.Contain("--replace"));
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "bob", "--replace"], services), Is.Zero);
        Assert.That(prompt.Calls, Is.EqualTo(1));
        Assert.That(store.Value?.Username, Is.EqualTo("bob"));
        Assert.That(store.Writes, Is.EqualTo(1));
        AssertNoSecret(console);
    }

    [Test]
    public async Task NoninteractiveAddRequiresExplicitStdin()
    {
        var store = new MemoryStore();
        var prompt = new TestPrompt();
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "alice"], services), Is.EqualTo(2));
        Assert.That(console.Errors.ToString(), Does.Contain("--password-stdin"));
        Assert.That(prompt.Calls + store.Writes, Is.Zero);
    }

    [Test]
    public async Task PromptLimitIsValidatedBeforeCallingStore()
    {
        var store = new MemoryStore();
        var console = new TestConsole(interactive: true);
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: new TestPrompt { Password = new string('x', 1281) }, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "alice"], services), Is.EqualTo(2));
        Assert.That(store.Writes, Is.Zero);
    }

    [Test]
    public async Task CapabilitiesAdvertiseCommandsAndNamedSourceWithoutStoreAccess()
    {
        var store = new MemoryStore { Failure = new IOException("sentinel") };
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["capabilities", "--json"], services), Is.Zero);
        using var result = System.Text.Json.JsonDocument.Parse(console.Output.ToString());
        Assert.That(result.RootElement.GetProperty("credentialCommands").EnumerateArray().Select(c => c.GetString()), Is.EqualTo(new[] { "add", "remove", "exists" }));
        Assert.That(result.RootElement.GetProperty("authenticationOptions").EnumerateArray().Select(c => c.GetString()), Does.Contain("--credential"));
        Assert.That(store.Calls, Is.Zero);
        AssertNoSecret(console);
    }

    [TestCase("../escape")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    [TestCase("é")]
    [TestCase(" a")]
    [TestCase("-a")]
    [TestCase("")]
    public void InvalidNamesAreRejected(string name)
        => Assert.Throws<ArgumentException>(() => CredentialName.Normalize(name));

    [Test]
    public void NameLengthAndCaseArePortable()
    {
        Assert.That(CredentialName.Normalize("A_b.0-Z"), Is.EqualTo("a_b.0-z"));
        Assert.That(CredentialName.Normalize(new string('a', 128)), Has.Length.EqualTo(128));
        Assert.Throws<ArgumentException>(() => CredentialName.Normalize(new string('a', 129)));
    }

    [TestCase("add")]
    [TestCase("remove")]
    [TestCase("exists")]
    public async Task InvalidNameDoesNotAccessStore(string verb)
    {
        var store = new MemoryStore();
        await using var services = CliComposition.CreateDefault(new TestConsole(), namedCredentialStore: store);
        string[] extra = verb == "add" ? ["--username", "alice", "--password-stdin"] : [];
        Assert.That(await CliHost.InvokeAsync(["credential", verb, "../escape", .. extra], services), Is.EqualTo(2));
        Assert.That(store.Calls, Is.Zero);
    }

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public async Task ExistsUsesMetadataOnly(bool present, int expected)
    {
        var store = new MemoryStore { Value = present ? new("alice", "sentinel") : null };
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "exists", "orders-dev"], services), Is.EqualTo(expected));
        Assert.That(store.Reads + store.Writes, Is.Zero);
        Assert.That(store.Calls, Is.EqualTo(1));
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Is.Empty);
    }

    [Test]
    public async Task RemovalIsIdempotent()
    {
        var store = new MemoryStore { Value = new("alice", "sentinel") };
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        for (var i = 0; i < 2; i++)
            Assert.That(await CliHost.InvokeAsync(["credential", "remove", "orders-dev"], services), Is.Zero);
        Assert.That(store.Value, Is.Null);
        Assert.That(store.Reads, Is.Zero);
        AssertNoSecret(console);
    }

    [TestCase("add")]
    [TestCase("remove")]
    [TestCase("exists")]
    public async Task BackendFailuresAreSafeAndDistinctFromAbsence(string verb)
    {
        var store = new MemoryStore { Failure = new IOException("sentinel") };
        var console = new TestConsole("sentinel");
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        string[] extra = verb == "add" ? ["--username", "alice", "--password-stdin"] : [];
        Assert.That(await CliHost.InvokeAsync(["credential", verb, "orders-dev", .. extra], services), Is.EqualTo(10));
        AssertNoSecret(console);
        Assert.That(console.Errors.ToString(), Does.Contain("unlocked"));
    }

    [Test]
    public async Task WriteFailureDoesNotExposePassword()
    {
        var store = new MemoryStore { WriteFailure = new ArgumentException("sentinel") };
        var console = new TestConsole("sentinel");
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "alice", "--password-stdin"], services), Is.EqualTo(2));
        Assert.That(store.Value, Is.Null);
        AssertNoSecret(console);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task CancelledInputNeverWrites(bool stdin)
    {
        var store = new MemoryStore();
        var prompt = new TestPrompt { Cancel = true };
        var console = new TestConsole(interactive: true) { Reader = new CancellingReader() };
        await using var services = CliComposition.CreateDefault(console, passwordPrompt: prompt, namedCredentialStore: store);
        string[] extra = stdin ? ["--password-stdin"] : [];
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "alice", .. extra], services), Is.EqualTo(130));
        Assert.That(store.Writes, Is.Zero);
    }

    [Test]
    public async Task PrecancelledCommandDoesNotAccessStore()
    {
        var store = new MemoryStore();
        await using var services = CliComposition.CreateDefault(new TestConsole(), namedCredentialStore: store);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.That(await CliHost.InvokeAsync(["credential", "exists", "orders-dev"], services, cancellation.Token), Is.EqualTo(130));
        Assert.That(store.Calls, Is.Zero);
    }

    [TestCase(1280, "\r\n", 0)]
    [TestCase(1281, "", 2)]
    [TestCase(4000, "\n", 2)]
    public async Task StdinLimitIsBounded(int length, string suffix, int expected)
    {
        var store = new MemoryStore();
        await using var services = CliComposition.CreateDefault(new TestConsole(new string('x', length) + suffix), namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["credential", "add", "orders-dev", "--username", "alice", "--password-stdin"], services), Is.EqualTo(expected));
        Assert.That(store.Writes, Is.EqualTo(expected == 0 ? 1 : 0));
    }

    [TestCase("--password-prompt")]
    [TestCase("--credential-store", "legacy")]
    public async Task NamedCredentialConflictsWithOtherSources(params string[] extra)
    {
        var store = new MemoryStore();
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console, namedCredentialStore: store);
        Assert.That(await CliHost.InvokeAsync(["connection", "check", "trino://localhost/app", "--credential", "orders-dev", .. extra], services), Is.EqualTo(2));
        Assert.That(store.Calls, Is.Zero);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NamedCredentialFlowsThroughExistingAuthenticationSource(bool mismatch)
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("NReco.PrestoAdo", NReco.PrestoAdo.PrestoDbFactory.Instance);
        var store = new MemoryStore { Value = new("alice", "sentinel") };
        var console = new TestConsole();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], namedCredentialStore: store);
        string[] extra = mismatch ? ["--username", "bob"] : [];
        var exit = await CliHost.InvokeAsync(["connection", "check", "trino://localhost/pg/public?UseSsl=true", "--credential", "Orders-Dev", "--output-format", "json", .. extra], services);
        Assert.That(exit, Is.EqualTo(mismatch ? 2 : 0));
        Assert.That(store.Reads, Is.EqualTo(1));
        Assert.That(store.LastName, Is.EqualTo("orders-dev"));
        AssertNoSecret(console);
    }

    [Test]
    public async Task NamedCredentialDryRunNeverAcquiresSecrets()
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", Microsoft.Data.Sqlite.SqliteFactory.Instance);
        var store = new MemoryStore { Failure = new IOException("sentinel") };
        var console = new TestConsole("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"customer\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\"}]}]}");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:test"] = "sqlite:///:memory:" }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)], namedCredentialStore: store);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--credential", "orders-dev", "--dry-run"], services);
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(store.Calls, Is.Zero);
    }

    private static void AssertNoSecret(TestConsole console)
        => Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));

    private sealed class MemoryStore : INamedCredentialStore
    {
        public UsernamePasswordCredentials? Value { get; set; }
        public Exception? Failure { get; init; }
        public Exception? WriteFailure { get; init; }
        public int Calls { get; private set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public string? LastName { get; private set; }
        private void Access(string name, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Calls++;
            LastName = name;
            if (Failure is not null) throw Failure;
        }
        public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default)
        { Access(name, cancellationToken); return new(Value is not null); }
        public ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
        { Access(reference, cancellationToken); Reads++; return new(Value ?? throw new CredentialStoreException()); }
        public ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default)
        {
            Access(name, cancellationToken);
            if (WriteFailure is not null) throw WriteFailure;
            if (Value is not null && !replace) throw new CredentialStoreException(true);
            Writes++;
            Value = credentials;
            return ValueTask.CompletedTask;
        }
        public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default)
        { Access(name, cancellationToken); Value = null; return ValueTask.CompletedTask; }
    }

    private sealed class TestPrompt : IHiddenPasswordPrompt
    {
        public int Calls { get; private set; }
        public bool Cancel { get; init; }
        public string Password { get; init; } = "sentinel";
        public ValueTask<string> ReadAsync(CancellationToken cancellationToken = default)
        { Calls++; if (Cancel) throw new OperationCanceledException(); return new(Password); }
    }
    private sealed class CancellingReader : StringReader
    {
        public CancellingReader() : base("") { }
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);
    }
    private sealed class TestConsole(string input = "", bool interactive = false) : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader Reader { get; init; } = new StringReader(input);
        public TextReader In => Reader;
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => !interactive;
        public bool IsInteractive => interactive;
    }
}
