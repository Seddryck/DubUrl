using DubUrl.Application;
using NUnit.Framework;

namespace DubUrl.Cli.Testing;

public class ProviderPackServiceTests
{
    [TestCase("connector-not-curated", CliExitCodes.ProviderNotFound)]
    [TestCase("connector-not-installed", CliExitCodes.ProviderNotFound)]
    [TestCase("connector-download-failed", CliExitCodes.ProviderOperationFailed)]
    [TestCase("connector-archive-invalid", CliExitCodes.ProviderOperationFailed)]
    [TestCase("api-incompatible", CliExitCodes.ProviderOperationFailed)]
    [TestCase("connector-install-failed", CliExitCodes.ProviderOperationFailed)]
    [TestCase("connector-list-failed", CliExitCodes.ProviderOperationFailed)]
    [TestCase("connector-uninstall-failed", CliExitCodes.ProviderOperationFailed)]
    public async Task ApplicationFailure_MapsDiagnosticAndExitCode(string code, int exitCode)
    {
        var service = new ProviderPackService(new FailingService(code));
        var list = Assert.Throws<ProviderPackCommandException>(() => service.List());
        var install = await Assert.ThrowsAsync<ProviderPackCommandException>(async () => await service.InstallAsync("pg", default));
        var uninstall = Assert.Throws<ProviderPackCommandException>(() => service.Uninstall("pg"));
        foreach (var exception in new[] { list, install, uninstall })
        {
            Assert.Multiple(() =>
            {
                Assert.That(exception!.Code, Is.EqualTo(code));
                Assert.That(exception.ExitCode, Is.EqualTo(exitCode));
                Assert.That(exception.Message, Is.EqualTo("safe diagnostic"));
            });
        }
    }

    [Test]
    public async Task Cancellation_PreservesHostCancellationContract()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new ProviderPackService(new FailingService("unused"));
        var exception = await Assert.ThrowsAsync<ConnectorOperationCanceledException>(
            async () => await service.InstallAsync("pg", cancellation.Token));
        Assert.That(exception!.CancellationToken, Is.EqualTo(cancellation.Token));
    }

    private sealed class FailingService(string code) : IConnectorService
    {
        public IReadOnlyList<ConnectorInfo> List() => throw new ConnectorOperationException(code, "safe diagnostic");
        public Task<ConnectorInfo> InstallAsync(string connector, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested) throw new ConnectorOperationCanceledException(cancellationToken);
            throw new ConnectorOperationException(code, "safe diagnostic");
        }
        public string Uninstall(string connector) => throw new ConnectorOperationException(code, "safe diagnostic");
    }
}
