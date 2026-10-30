using System.Data.Common;
using System.Reflection;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using Moq.Protected;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

public class OptionalConnectionOptionsTest
{
    private static ConnectionConfigurationCapability All => new("Application Name", "Pooling", "Min Pool Size", "Max Pool Size", "Connection Timeout", "Command Timeout");

    private static ConnectionUrl Create(ConnectionConfigurationCapability? capability = null,
        Mock<DbProviderFactory>? factory = null, string connectionString = "")
    {
        factory ??= new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new DbConnectionStringBuilder());
        var registry = new Mock<ISchemeRegistry>();
        registry.Setup(x => x.Resolve(It.IsAny<ConnectionUri>())).Returns(new ResolvedConnection(connectionString,
            Mock.Of<IDialect>(), Mock.Of<IConnectivity>(), Mock.Of<IParametrizer>(), factory.Object));
        registry.Setup(x => x.TryGetCapability(It.IsAny<SchemeKey>(), out capability)).Returns(capability is not null);
        return new ConnectionUrl("test://alice:redaction-marker@localhost/db", registry.Object);
    }

    [Test]
    public void OptionalBuilder_ExposesOnlyAllowedMethods()
    {
        var methods = typeof(OptionalConnectionOptions).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.That(methods.Select(method => method.Name), Is.EquivalentTo(new[]
            { "WithApplicationName", "WithPooling", "WithConnectionTimeout", "WithCommandTimeout" }));
        Assert.That(typeof(OptionalConnectionOptions).GetConstructors(), Is.Empty);
        Assert.That(typeof(OptionalConnectionOptions).IsSealed, Is.True);
        Assert.That(typeof(OptionalConnectionOptions).GetProperties(), Is.Empty);
    }

    [TestCase(ConnectionConfigurationOption.ApplicationName)]
    [TestCase(ConnectionConfigurationOption.Pooling)]
    [TestCase(ConnectionConfigurationOption.ConnectionTimeout)]
    [TestCase(ConnectionConfigurationOption.CommandTimeout)]
    public void StrictUnsupportedSetting_FailsBeforeConnectionCreation(ConnectionConfigurationOption option)
    {
        var factory = new Mock<DbProviderFactory>();
        var target = Configure(Create(factory: factory), option);
        var error = Assert.Throws<NotSupportedException>(() => target.Open());
        Assert.That(error!.Message, Does.Contain(option.ToString()).And.Contain("test").And.Contain("provider"));
        Assert.That(error.ToString(), Does.Not.Contain("redaction-marker"));
        factory.Verify(x => x.CreateConnection(), Times.Never);
    }

    private static ConnectionUrl Configure(ConnectionUrl url, ConnectionConfigurationOption option) => option switch
    {
        ConnectionConfigurationOption.ApplicationName => url.WithApplicationName("Reporting"),
        ConnectionConfigurationOption.Pooling => url.WithPooling(new()),
        ConnectionConfigurationOption.ConnectionTimeout => url.WithConnectionTimeout(TimeSpan.FromSeconds(15)),
        ConnectionConfigurationOption.CommandTimeout => url.WithCommandTimeout(TimeSpan.FromSeconds(30)),
        _ => throw new ArgumentOutOfRangeException(nameof(option))
    };

    [Test]
    public void AllSupported_UsesNativeDefaultsAndPreservesOriginal()
    {
        var original = Create(All, connectionString: "Database=app;Application Name=URL");
        var previous = original.Parse(); // Configuring an already resolved URL must not reuse its cache.
        var target = original.WithOptional(options => options.WithApplicationName("Reporting;Password=literal")
            .WithPooling(new() { MinSize = 2, MaxSize = 10 })
            .WithConnectionTimeout(TimeSpan.FromMilliseconds(100))
            .WithCommandTimeout(TimeSpan.Zero));
        var builder = new DbConnectionStringBuilder { ConnectionString = target.Parse() };
        Assert.That(builder["Application Name"], Is.EqualTo("Reporting;Password=literal"));
        Assert.That(builder["Database"], Is.EqualTo("app"));
        Assert.That(builder["Connection Timeout"], Is.EqualTo("1"));
        Assert.That(builder["Command Timeout"], Is.EqualTo("0"));
        Assert.That(builder["Min Pool Size"], Is.EqualTo("2"));
        Assert.That(builder["Max Pool Size"], Is.EqualTo("10"));
        Assert.That(builder.ContainsKey("Password"), Is.False);
        Assert.That(target.SkippedOptions, Is.Empty);
        Assert.That(original.Parse(), Is.EqualTo(previous));
        Assert.That(original.Url, Is.EqualTo(target.Url));
    }

    [Test]
    public void NoneSupported_SkipsIndependentlyWithoutChangingNativeOptions()
    {
        var original = "Database=app;Pooling=true";
        var target = Create(connectionString: original).WithOptional(options => options
            .WithApplicationName("Reporting").WithPooling(new() { Enabled = false })
            .WithConnectionTimeout(TimeSpan.FromSeconds(10)).WithCommandTimeout(TimeSpan.FromSeconds(20)));
        Assert.That(target.Parse(), Is.EqualTo(original));
        Assert.That(target.SkippedOptions.Select(skip => skip.Option), Is.EquivalentTo(new[] { ConnectionConfigurationOption.ApplicationName,
            ConnectionConfigurationOption.Pooling, ConnectionConfigurationOption.ConnectionTimeout, ConnectionConfigurationOption.CommandTimeout }));
        Assert.That(string.Join(" ", target.SkippedOptions), Does.Not.Contain("redaction-marker"));
    }

    [Test]
    public void MixedSupport_AppliesSupportedSettingsAndSkipsTheRest()
    {
        var target = Create(new(pooling: "Pooling")).WithOptional(options => options
            .WithApplicationName("Reporting").WithPooling(new() { Enabled = false }).WithCommandTimeout(TimeSpan.FromSeconds(5)));
        var builder = new DbConnectionStringBuilder { ConnectionString = target.Parse() };
        Assert.That(builder["Pooling"], Is.EqualTo("False"));
        Assert.That(target.SkippedOptions.Select(skip => skip.Option), Is.EquivalentTo(new[]
            { ConnectionConfigurationOption.ApplicationName, ConnectionConfigurationOption.CommandTimeout }));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RequiredSetting_WinsOverOptionalInEitherOrder(bool requiredFirst)
    {
        var original = Create(All, connectionString: "Application Name=URL");
        var target = requiredFirst
            ? original.WithApplicationName("Required").WithOptional(options => options.WithApplicationName("Optional"))
            : original.WithOptional(options => options.WithApplicationName("Optional")).WithApplicationName("Required");
        var builder = new DbConnectionStringBuilder { ConnectionString = target.Parse() };
        Assert.That(builder["Application Name"], Is.EqualTo("Required"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void OptionalRequest_CannotHideUnsupportedRequiredRequest(bool requiredFirst)
    {
        var original = Create(new(pooling: "Pooling"));
        var required = new ConnectionPoolingOptions { MaxSize = 20 };
        var target = requiredFirst
            ? original.WithPooling(required).WithOptional(options => options.WithPooling(new()))
            : original.WithOptional(options => options.WithPooling(new())).WithPooling(required);
        Assert.Throws<NotSupportedException>(() => target.Parse());
    }

    [Test]
    public void RepeatedCalls_LastOfSamePolicyWinsAndCapturedBuilderCannotMutateTarget()
    {
        OptionalConnectionOptions? captured = null;
        var called = false;
        var target = Create(All).WithOptional(options => { called = true; captured = options; options.WithApplicationName("First"); })
            .WithOptional(options => options.WithApplicationName("Last"));
        captured!.WithApplicationName("Too late");
        Assert.That(called, Is.True);
        Assert.That(new DbConnectionStringBuilder { ConnectionString = target.Parse() }["Application Name"], Is.EqualTo("Last"));
        target = target.WithApplicationName("Required first").WithApplicationName("Required last");
        Assert.That(new DbConnectionStringBuilder { ConnectionString = target.Parse() }["Application Name"], Is.EqualTo("Required last"));
    }

    [Test]
    public void OptionalPoolLimits_UnsupportedLimitsSkipWholeSetting()
    {
        var target = Create(new(pooling: "Pooling"), connectionString: "Pooling=true")
            .WithOptional(options => options.WithPooling(new() { MaxSize = 10 }));
        Assert.That(target.Parse(), Is.EqualTo("Pooling=true"));
        Assert.That(target.SkippedOptions.Single().Option, Is.EqualTo(ConnectionConfigurationOption.Pooling));
    }

    [Test]
    public void OptionalInvalidValues_AreValidatedEvenWithoutSupport()
    {
        var target = Create();
        Assert.Throws<ArgumentException>(() => target.WithOptional(options => options.WithApplicationName(" ")));
        Assert.Throws<ArgumentNullException>(() => target.WithOptional(options => options.WithPooling(null!)));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WithOptional(options => options.WithPooling(new() { MaxSize = 0 })));
        Assert.Throws<ArgumentException>(() => target.WithOptional(options => options.WithPooling(new() { MinSize = 4, MaxSize = 2 })));
        Assert.Throws<ArgumentException>(() => target.WithOptional(options => options.WithPooling(new() { Enabled = false, MaxSize = 2 })));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WithOptional(options => options.WithConnectionTimeout(TimeSpan.FromSeconds(-1))));
        Assert.Throws<ArgumentOutOfRangeException>(() => target.WithOptional(options => options.WithCommandTimeout(TimeSpan.MaxValue)));
        Assert.Throws<ArgumentNullException>(() => target.WithOptional(null!));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SupportedConfigurationFailure_IsNotSwallowed(bool optional)
    {
        var factory = new Mock<DbProviderFactory>();
        var original = Create(All, factory);
        factory.Setup(x => x.CreateConnectionStringBuilder()).Throws(new NotSupportedException("Provider operation failed"));
        var target = optional ? original.WithOptional(options => options.WithApplicationName("Reporting")) : original.WithApplicationName("Reporting");
        Assert.That(Assert.Throws<NotSupportedException>(() => target.Parse())!.Message, Is.EqualTo("Provider operation failed"));
    }

    [Test]
    public void ConnectionFailure_AfterOptionalConfigurationDisposesConnectionAndPropagates()
    {
        var connection = new ConnectionSourceTest.TestConnection { FailOpen = true };
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnection()).Returns(connection);
        var target = Create(All, factory).WithOptional(options => options.WithApplicationName("Reporting"));
        Assert.Throws<InvalidOperationException>(() => target.Open());
        Assert.That(connection.Disposals, Is.EqualTo(1));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Cancellation_IsNeverConvertedToOptionalFallback(bool supported)
    {
        var cancellation = new OperationCanceledException(new CancellationToken(true));
        var connection = new Mock<DbConnection>();
        connection.Setup(x => x.Open()).Throws(cancellation);
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnection()).Returns(connection.Object);
        var target = Create(supported ? All : null, factory)
            .WithOptional(options => options.WithApplicationName("Reporting"));
        Assert.That(Assert.Throws<OperationCanceledException>(() => target.Open()), Is.SameAs(cancellation));
        connection.Protected().Verify("Dispose", Times.Once(), ItExpr.Is<bool>(disposing => disposing));
    }
}
