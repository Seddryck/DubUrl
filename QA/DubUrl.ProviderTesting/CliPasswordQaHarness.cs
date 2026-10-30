using System.Text.Json;
using DubUrl.Application;
using DubUrl.Authentication;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace DubUrl.ProviderTesting;

internal sealed class CliPasswordQaHarness : IAsyncDisposable
{
    private readonly ServiceProvider services;
    private readonly IConfigurationRoot configuration;
    private readonly RegistryServices facade;
    private readonly QaConsole console;
    private readonly string location;
    internal PasswordQaRegistry Observed { get; }
    internal INamedCredentialStore NativeStore => services.GetRequiredService<INamedCredentialStore>();

    internal CliPasswordQaHarness(PasswordQaDriver driver, CliPasswordQaAdapters? adapters = null,
        bool interactive = false, string? location = null, string document = "")
    {
        this.location = location ?? driver.WithOptions(false);
        Observed = new(driver.Registry);
        console = new(interactive, document);
        configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:qa"] = this.location,
            ["ProviderPacks:StorePath"] = Path.Combine(Path.GetTempPath(), "DubUrlQaUnusedPacks", Guid.NewGuid().ToString("N"))
        }).Build();
        services = CliComposition.CreateDefault(console, configuration, passwordPrompt: adapters, namedCredentialStore: adapters);
        facade = new(services, Observed);
    }

    internal async Task<CliPasswordQaResult> CheckAsync(string[] options, bool json = true, string selector = "named", CancellationToken cancellationToken = default)
    {
        var variable = "DUBURL_CLI_AUTH_QA_" + Guid.NewGuid().ToString("N");
        if (selector == "environment") Environment.SetEnvironmentVariable(variable, location);
        try
        {
            string[] target = selector switch
            { "environment" => ["--connection-env", variable], "url" => [location], _ => ["--connection", "qa"] };
            string[] format = json ? ["--output-format", "json"] : [];
            var exit = await CliHost.InvokeAsync(["connection", "check", .. target, .. format, .. options], facade, cancellationToken);
            return new(exit, console.Output.ToString(), console.Errors.ToString());
        }
        finally { if (selector == "environment") Environment.SetEnvironmentVariable(variable, null); }
    }

    internal async Task<CliPasswordQaResult> DeployAsync(string[] options, bool dryRun = false, CancellationToken cancellationToken = default)
    {
        string[] dry = dryRun ? ["--dry-run"] : [];
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "qa", "--output-format", "json", .. dry, .. options], facade, cancellationToken);
        return new(exit, console.Output.ToString(), console.Errors.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        await services.DisposeAsync();
        (configuration as IDisposable)?.Dispose();
        console.Output.Dispose();
        console.Errors.Dispose();
    }

    private sealed class RegistryServices(IServiceProvider inner, ISchemeRegistry registry) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(ISchemeRegistry) ? registry : inner.GetService(type);
    }
    private sealed class QaConsole(bool interactive, string input) : ICliConsole
    {
        internal StringWriter Output { get; } = new();
        internal StringWriter Errors { get; } = new();
        public TextReader In { get; } = new StringReader(input);
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInputRedirected => true;
        public bool IsInteractive => interactive;
    }
}

internal sealed record CliPasswordQaResult(int ExitCode, string Output, string Error)
{
    internal void AssertSafe(string? password)
    {
        if (string.IsNullOrEmpty(password)) return;
        var text = Output + Error;
        Assert.That(text.Contains(password, StringComparison.Ordinal), Is.False, "CLI output exposed a password.");
        Assert.That(text.Contains(Uri.EscapeDataString(password), StringComparison.Ordinal), Is.False, "CLI output exposed an encoded password.");
    }
    internal void AssertJson(bool success)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(Output); }
        catch (JsonException) { throw new AssertionException("CLI output must be a valid JSON envelope; raw output is suppressed."); }
        using (document) Assert.That(document.RootElement.GetProperty("success").GetBoolean(), Is.EqualTo(success));
    }
}

/// <summary>Controlled source adapters; positive cases still run the real CLI and native database I/O.</summary>
internal sealed class CliPasswordQaAdapters(UsernamePasswordCredentials credentials) : IHiddenPasswordPrompt, INamedCredentialStore
{
    public ValueTask<bool> ExistsAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask AddAsync(string name, UsernamePasswordCredentials credentials, bool replace, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public ValueTask RemoveAsync(string name, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    internal string? Failure { get; init; }
    internal bool Block { get; init; }
    internal int PromptCalls { get; private set; }
    internal int StoreCalls { get; private set; }
    internal int Calls => PromptCalls + StoreCalls;
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async ValueTask<string> ReadAsync(CancellationToken cancellationToken = default)
    {
        PromptCalls++;
        await ResolveAsync(cancellationToken);
        return credentials.Password ?? "";
    }
    public async ValueTask<UsernamePasswordCredentials> ReadAsync(string reference, CancellationToken cancellationToken = default)
    {
        StoreCalls++;
        await ResolveAsync(cancellationToken);
        return credentials;
    }
    private async Task ResolveAsync(CancellationToken cancellationToken)
    {
        Entered.TrySetResult();
        if (Failure is not null) throw new InvalidOperationException("qa-sensitive-store-sentinel: " + Failure);
        if (Block) await Task.Delay(Timeout.Infinite, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }
    public override string ToString() => "CliPasswordQaAdapters [REDACTED]";
}
