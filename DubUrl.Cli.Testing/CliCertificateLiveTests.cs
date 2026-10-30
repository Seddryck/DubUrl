using System.Data.Common;
using DubUrl.Cli;
using DubUrl.Mapping;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

/// <summary>Requires an externally provisioned certificate-only account, server trust, and fixture files.</summary>
[NonParallelizable, Category("ClientCertificateLive")]
public class CliCertificateLiveTests
{
    [TestCase("PEM"), TestCase("PFX"), TestCase("P12")]
    public async Task CertificateCliQueriesLiveServer(string form)
    {
        var url = Required("URL");
        var path = Required(form);
        var ca = Required("CA");
        string[] key = form == "PEM" ? ["--client-key", Required("KEY")] : [];
        var passwordReference = Environment.GetEnvironmentVariable("DUBURL_CLI_CERT_PASSWORD_REFERENCE");
        string[] password = passwordReference is null ? [] : ["--certificate-credential", passwordReference];
        var exit = await Invoke(["connection", "check", url, "--client-certificate", path, .. key, "--ca-certificate", ca, .. password, "--engine", "--output-format", "json"]);
        Assert.That(exit, Is.Zero, "Live certificate login/query failed; private driver diagnostics are suppressed.");
    }
    [TestCase("rejected-client"), TestCase("untrusted-server"), TestCase("hostname")]
    public async Task CertificateCliRejectsLiveTlsFailures(string failure)
    {
        var url = failure == "hostname" ? Required("WRONG_HOST_URL") : Required("URL");
        var certificate = Required(failure == "rejected-client" ? "REJECTED_PEM" : "PEM");
        var key = Required(failure == "rejected-client" ? "REJECTED_KEY" : "KEY");
        var ca = Required(failure == "untrusted-server" ? "UNTRUSTED_CA" : "CA");
        var exit = await Invoke(["connection", "check", url, "--client-certificate", certificate, "--client-key", key, "--ca-certificate", ca, "--engine", "--output-format", "json"]);
        Assert.That(exit, Is.EqualTo(CliExitCodes.ConnectionCheckFailed), "A fresh connection must reject invalid client/server identity.");
    }
    private static string Required(string suffix)
    {
        var value = Environment.GetEnvironmentVariable("DUBURL_CLI_CERT_" + suffix);
        if (string.IsNullOrWhiteSpace(value)) Assert.Ignore("Missing isolated CLI certificate fixture: DUBURL_CLI_CERT_" + suffix + ". No live certificate login was verified.");
        return value!;
    }
    private static async Task<int> Invoke(string[] arguments)
    {
        DbProviderFactories.RegisterFactory("Npgsql", Npgsql.NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnector.MySqlConnectorFactory.Instance);
        DbProviderFactories.RegisterFactory("SingleStoreConnector", SingleStoreConnector.SingleStoreConnectorFactory.Instance);
        var console = new QuietConsole();
        await using var services = CliComposition.CreateDefault(console, providerModules: [new AttributeProviderModule(new AssemblyTypesProbe().Assemblies)]);
        return await CliHost.InvokeAsync(arguments, services);
    }
    private sealed class QuietConsole : ICliConsole
    {
        public TextReader In => TextReader.Null;
        public TextWriter Out => TextWriter.Null;
        public TextWriter Error => TextWriter.Null;
        public bool IsInteractive => false;
        public bool IsInputRedirected => true;
    }
}
