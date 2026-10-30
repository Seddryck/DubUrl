using System.Data.Common;
using DubUrl.Application;
using DubUrl.Cli;
using DubUrl.Mapping;
using Microsoft.Extensions.Configuration;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

[NonParallelizable]
public class SharedConnectionOptionsTests
{
    private const string Url = "postgres://alice@db.example/app";
    private static readonly string[] Options = ["--tls", "--ca-certificate", "absent-server-ca.pem", "--connection-timeout", "500ms",
        "--command-timeout", "infinite", "--application-name", "Reporting;literal=1", "--time-zone", "Europe/Brussels",
        "--pooling", "enabled", "--pool-min-size", "1", "--pool-max-size", "12", "--pool-idle-timeout", "30s", "--pool-max-lifetime", "2m"];

    [TestCase("direct"), TestCase("named"), TestCase("environment")]
    public async Task CheckUsesSharedLibraryForEveryConnectionSource(string selection)
    {
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        var console = new ConsoleStub();
        var check = new Check();
        Environment.SetEnvironmentVariable("DUBURL_OPTIONS_TEST", Url);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:test"] = Url }).Build();
            await using var services = CliComposition.CreateDefault(console, configuration,
                providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
            string[] selector = selection switch { "named" => ["--connection", "test"], "environment" => ["--connection-env", "DUBURL_OPTIONS_TEST"], _ => [Url] };
            var exit = await CliHost.InvokeAsync(["connection", "check", .. selector, "--engine", .. Options], new CheckServices(services, check));
            Assert.That(exit, Is.Zero, console.Errors.ToString());
            Assert.That(check.Calls, Is.EqualTo(1));
        }
        finally { Environment.SetEnvironmentVariable("DUBURL_OPTIONS_TEST", null); }
    }

    [TestCase("--connection-timeout", "15")]
    [TestCase("--command-timeout", "-1s")]
    [TestCase("--connection-timeout", "999999999999999999h")]
    [TestCase("--pooling", "perhaps")]
    [TestCase("--pool-max-size", "0")]
    [TestCase("--time-zone", "UTC';sentinel")]
    [TestCase("--tls", "--pooling", "disabled", "--pool-max-size", "3")]
    [TestCase("--tls", "--tls")]
    public async Task InvalidValuesFailSafelyBeforeCallingCheck(params string[] options)
    {
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        var console = new ConsoleStub();
        var check = new Check();
        await using var services = CliComposition.CreateDefault(console,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
        var exit = await CliHost.InvokeAsync(["connection", "check", Url, "--output-format", "json", .. options], new CheckServices(services, check));
        Assert.That(exit, Is.EqualTo(2));
        Assert.That(check.Calls, Is.Zero);
        Assert.That(console.Output.ToString() + console.Errors.ToString(), Does.Not.Contain("sentinel"));
    }

    [Test]
    public async Task SchemaDryRunValidatesWithoutLoadingCaOrOpeningConnection()
    {
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        var console = new ConsoleStub();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:test"] = Url }).Build();
        await using var services = CliComposition.CreateDefault(console, configuration,
            providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
        var exit = await CliHost.InvokeAsync(["schema", "deploy", "-", "--input-format", "json", "--connection", "test", "--dry-run", .. Options], services);
        Assert.That(exit, Is.Zero, console.Errors.ToString());
        Assert.That(console.Output.ToString(), Does.Contain("CREATE TABLE"));
    }

    [TestCase("connection", "check"), TestCase("schema", "deploy"), TestCase("schema", "inspect")]
    public async Task HelpAdvertisesAllSharedOptionsWithoutConnection(string group, string command)
    {
        var console = new ConsoleStub();
        await using var services = CliComposition.CreateDefault(console);
        var parsed = CliRootCommandFactory.Create(services, default).Parse([group, command, "--help"]);
        Assert.That(parsed.Errors, Is.Empty);
        Assert.That(await CliHost.InvokeAsync([group, command, "--help"], services), Is.Zero);
        foreach (var option in Options.Where(v => v.StartsWith("--")))
            Assert.That(parsed.CommandResult.Command.Options.Select(o => o.Name), Does.Contain(option));
    }

    private sealed class CheckServices(IServiceProvider inner, Check check) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(IConnectionCheckService) ? check : inner.GetService(type);
    }
    private sealed class Check : IConnectionCheckService
    {
        internal int Calls { get; private set; }
        public async Task<ConnectionCheckResult> CheckAsync(ConnectionUrl target, ConnectionCheckOptions options, CancellationToken cancellationToken = default)
        {
            Calls++;
            await using var connection = (NpgsqlConnection)await target.ConnectAsync(cancellationToken);
            var native = new NpgsqlConnectionStringBuilder(connection.ConnectionString);
            Assert.That(native.SslMode, Is.EqualTo(SslMode.VerifyFull));
            Assert.That(native.RootCertificate, Is.EqualTo(Path.GetFullPath("absent-server-ca.pem")));
            Assert.That(native.Timeout, Is.EqualTo(1));
            Assert.That(native.ApplicationName, Is.EqualTo("Reporting;literal=1"));
            Assert.That(native.Timezone, Is.EqualTo("Europe/Brussels"));
            Assert.That(native.MinPoolSize, Is.EqualTo(1));
            Assert.That(native.MaxPoolSize, Is.EqualTo(12));
            Assert.That(native.ConnectionIdleLifetime, Is.EqualTo(30));
            Assert.That(native.ConnectionLifetime, Is.EqualTo(120));
            using var command = connection.CreateCommand();
            Assert.That(command.CommandTimeout, Is.Zero);
            Assert.That(connection.State, Is.EqualTo(System.Data.ConnectionState.Closed));
            Assert.That(target.Url, Is.EqualTo(Url));
            return new(true, "engine", "Npgsql", 0, []);
        }
    }
    private sealed class ConsoleStub : ICliConsole
    {
        internal StringWriter Output { get; } = new();
        internal StringWriter Errors { get; } = new();
        public TextReader In => new StringReader("{\"version\":1,\"tables\":[{\"identity\":{\"name\":\"customer\"},\"columns\":[{\"name\":\"id\",\"type\":\"int32\"}]}]}");
        public TextWriter Out => Output;
        public TextWriter Error => Errors;
        public bool IsInteractive => false;
        public bool IsInputRedirected => true;
    }
}
