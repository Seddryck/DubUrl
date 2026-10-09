using DubUrl.Locating.OdbcDriver;
using Microsoft.Win32;
using NUnit.Framework;

#pragma warning disable CA1416 // Registry types are only values in the platform-neutral fake.

namespace DubUrl.Testing.Locating.OdbcDriver;

public class DriverListerTest
{
    private sealed class FakeStrategy(params string[] drivers) : IDriverListingStrategy
    {
        public string[] List() => drivers;
    }

    private sealed class FakeRegistryReader : IRegistryDriverReader
    {
        public List<RegistryHive> RequestedHives { get; } = [];

        public string[] List(RegistryHive hive)
        {
            RequestedHives.Add(hive);
            return hive == RegistryHive.LocalMachine
                ? ["System driver"]
                : ["User driver"];
        }
    }

    private sealed class FakeCommandRunner(CommandResult result) : ICommandRunner
    {
        public string? FileName { get; private set; }
        public string[] Arguments { get; private set; } = [];

        public CommandResult Run(string fileName, params string[] arguments)
        {
            FileName = fileName;
            Arguments = arguments;
            return result;
        }
    }

    private sealed class ThrowingCommandRunner : ICommandRunner
    {
        public CommandResult Run(string fileName, params string[] arguments)
            => throw new InvalidOperationException("Missing executable.");
    }

    [Test]
    public void List_InjectedStrategy_ReturnsDrivers()
    {
        var lister = new DriverLister(new FakeStrategy("Driver A", "Driver B"));

        var result = lister.List();

        Assert.That(result, Is.EqualTo(new[] { "Driver A", "Driver B" }));
    }

    [Test]
    public void List_WindowsRegistry_ReturnsMachineAndUserDrivers()
    {
        var reader = new FakeRegistryReader();
        var strategy = new WindowsRegistryDriverListingStrategy(reader);

        var result = strategy.List();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new[] { "System driver", "User driver" }));
            Assert.That(reader.RequestedHives, Is.EqualTo(new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser }));
        });
    }

    [Test]
    public void List_UnixOdbc_ParsesAndNormalizesDriverNames()
    {
        var runner = new FakeCommandRunner(new CommandResult(
            0,
            "[PostgreSQL ANSI]\n[PostgreSQL Unicode]\n[PostgreSQL Unicode]\nwarning",
            string.Empty));
        var strategy = new UnixOdbcDriverListingStrategy(runner);

        var result = strategy.List();

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(new[] { "PostgreSQL ANSI", "PostgreSQL Unicode" }));
            Assert.That(runner.FileName, Is.EqualTo("odbcinst"));
            Assert.That(runner.Arguments, Is.EqualTo(new[] { "-q", "-d" }));
        });
    }

    [Test]
    public void List_UnixOdbcWithoutDrivers_ReturnsEmpty()
    {
        var strategy = new UnixOdbcDriverListingStrategy(
            new FakeCommandRunner(new CommandResult(0, string.Empty, string.Empty)));

        var result = strategy.List();

        Assert.That(result, Is.Empty);
    }

    [Test]
    public void List_UnixOdbcFailure_ThrowsDiscoveryException()
    {
        var strategy = new UnixOdbcDriverListingStrategy(
            new FakeCommandRunner(new CommandResult(1, string.Empty, "configuration unavailable")));

        var exception = Assert.Throws<DriverDiscoveryException>(() => strategy.List());

        Assert.That(exception!.Message, Does.Contain("configuration unavailable"));
    }

    [Test]
    public void List_UnixOdbcMissingExecutable_ThrowsDiscoveryException()
    {
        var strategy = new UnixOdbcDriverListingStrategy(new ThrowingCommandRunner());

        var exception = Assert.Throws<DriverDiscoveryException>(() => strategy.List());

        Assert.That(exception!.Message, Does.Contain("odbcinst"));
    }
}

#pragma warning restore CA1416
