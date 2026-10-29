using System.Security.Cryptography;
using System.Text.Json;
using DubUrl.Authentication;
using DubUrl.Mapping;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

/// <summary>Opt-in: touches only a random fixture-owned entry and always removes it.</summary>
[Category("NativeCredentialStore")]
public class NativeNamedCredentialStoreTests
{
    [Test]
    public async Task NativeLifecycleAndCliCredentialResolution()
    {
        if (Environment.GetEnvironmentVariable("DUBURL_TEST_NATIVE_CREDENTIALS") != "1")
            Assert.Ignore("Set DUBURL_TEST_NATIVE_CREDENTIALS=1 with an unlocked current-user native store to run this smoke test.");
        var name = "test-" + Guid.NewGuid().ToString("N");
        var secret = " \t" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)) + "é\n\0尾 ";
        var connectionSecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));
        var console = new NativeConsole(secret + "\r\n");
        System.Data.Common.DbProviderFactories.RegisterFactory("NReco.PrestoAdo", NReco.PrestoAdo.PrestoDbFactory.Instance);
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(typeof(DubUrl.Provider.Trino.Mapping.TrinoMapper).Assembly)]);
        var store = services.GetRequiredService<INamedCredentialStore>();
        try
        {
            Assert.That(await store.ExistsAsync(name), Is.False);
            Assert.That(await CliHost.InvokeAsync(["credential", "exists", name], services), Is.EqualTo(1));
            Assert.That(await CliHost.InvokeAsync(["credential", "add", name, "--username", "alice", "--password-stdin"], services), Is.Zero, console.Errors.ToString());
            Assert.That(await CliHost.InvokeAsync(["credential", "exists", name.ToUpperInvariant()], services), Is.Zero);
            var first = await store.ReadAsync(name);
            Assert.That(first.Username, Is.EqualTo("alice"));
            Assert.That(first.Password == secret, Is.True, "Native password round trip failed.");
            await Assert.ThrowsAsync<CredentialStoreException>(async () => await store.AddAsync(name, new("bob", "unused"), false));
            await store.AddAsync(name, new("bob", connectionSecret), true);
            var changed = await store.ReadAsync(name);
            Assert.That(changed.Username, Is.EqualTo("bob"));
            Assert.That(changed.Password == connectionSecret, Is.True, "Replacement changed the password.");
            // NReco Open changes state without network I/O. This proves shared credential-source/mapping
            // integration, not a real database login.
            console.Output.GetStringBuilder().Clear();
            var exit = await CliHost.InvokeAsync(["connection", "check", "trino://localhost/pg/public?UseSsl=true", "--credential", name, "--output-format", "json"], services);
            using var result = JsonDocument.Parse(console.Output.ToString());
            var codes = result.RootElement.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString());
            Assert.That(exit, Is.Zero, "Credential mapping diagnostic codes: " + string.Join(",", codes));
            await store.AddAsync(name, new("bob", ""), true);
            Assert.That((await store.ReadAsync(name)).Password == "", Is.True, "Empty password round trip failed.");
            Assert.That(await CliHost.InvokeAsync(["credential", "remove", name], services), Is.Zero);
            Assert.That(await CliHost.InvokeAsync(["credential", "remove", name], services), Is.Zero);
            Assert.That(await CliHost.InvokeAsync(["credential", "exists", name], services), Is.EqualTo(1));
            Assert.That((console.Output.ToString() + console.Errors.ToString()).Contains(secret, StringComparison.Ordinal), Is.False, "Secret appeared in CLI output.");
            Assert.That((console.Output.ToString() + console.Errors.ToString()).Contains(connectionSecret, StringComparison.Ordinal), Is.False, "Secret appeared in CLI output.");
        }
        finally { await store.RemoveAsync(name); }
    }

    [Test]
    public async Task ConcurrentCreateHasOneWinner()
    {
        if (Environment.GetEnvironmentVariable("DUBURL_TEST_NATIVE_CREDENTIALS") != "1") Assert.Ignore("Native store smoke tests are opt-in.");
        var name = "test-" + Guid.NewGuid().ToString("N");
        await using var services = CliComposition.CreateDefault(new NativeConsole(""));
        var store = services.GetRequiredService<INamedCredentialStore>();
        try
        {
            async Task<bool> AddAsync()
            {
                try { await store.AddAsync(name, new("alice", "test-only"), false); return true; }
                catch (CredentialStoreException exception) when (exception.AlreadyExists) { return false; }
            }
            var results = await Task.WhenAll(AddAsync(), AddAsync());
            Assert.That(results.Count(result => result), Is.EqualTo(1));
        }
        finally { await store.RemoveAsync(name); }
    }

    [Test]
    public async Task PrecancelledNativeOperationDoesNotWrite()
    {
        // No OS entry is accessed: this also runs on hosts without native storage.
        await using var services = CliComposition.CreateDefault(new NativeConsole(""));
        var store = services.GetRequiredService<INamedCredentialStore>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThatAsync(async () =>
            await store.AddAsync("test-" + Guid.NewGuid().ToString("N"), new("alice", "test-only"), false, cancellation.Token), Throws.InstanceOf<OperationCanceledException>());
    }

    private sealed class NativeConsole(string input) : ICliConsole
    {
        public StringWriter Output { get; } = new();
        public StringWriter Errors { get; } = new();
        public TextReader In { get; } = new StringReader(input);
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => true;
        public bool IsInteractive => false;
    }
}
