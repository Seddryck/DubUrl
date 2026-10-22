using DubUrl.Extensions.DependencyInjection;
using DubUrl.MicroOrm;
using DubUrl.Querying;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NUnit.Framework;

namespace DubUrl.Extensions.Testing.DependencyInjection;

public class ServiceOptionsTest
{
    [Test]
    public void Defaults_SelectBaseFactoryNullLoggingAndDefaultReflectionCache()
    {
        using var provider = new ServiceCollection().AddDubUrl().BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value;
        var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
        Assert.Multiple(() =>
        {
            Assert.That(options.DatabaseUrlFactoryType, Is.EqualTo(typeof(DubUrl.DatabaseUrlFactory)));
            Assert.That(factory, Is.TypeOf<DubUrl.DatabaseUrlFactory>());
            Assert.That(factory.QueryLogger, Is.SameAs(NullQueryLogger.Instance));
            Assert.That(provider.GetRequiredService<IReflectionCache>(), Is.TypeOf<ReflectionCache>());
        });
    }

    [TestCase("instance")]
    [TestCase("action")]
    [TestCase("typed")]
    [TestCase("fluent")]
    [TestCase("fluent-before")]
    public void ConfigurationPaths_ConfigureTheActualFactoryLoggerAndCache(string mode)
    {
        var settings = new Settings(Mock.Of<IQueryLogger>(), Mock.Of<IReflectionCache>(), new RecordingLogger());
        var services = new ServiceCollection().AddSingleton(settings);
        services.Configure<DubUrlServiceOptions>(options => options.Logger = settings.Logger);
        switch (mode)
        {
            case "instance":
                var options = new DubUrlServiceOptions();
                Apply(options, settings);
                services.AddDubUrl(options);
                break;
            case "action": services.AddDubUrl(options => Apply(options, settings)); break;
            case "typed": services.AddDubUrl<TypedConfiguration>(); break;
            case "fluent":
                services.AddDubUrl().WithMicroOrm().WithQueryLogger(settings.QueryLogger).WithReflectionCache(settings.Cache);
                break;
            default:
                services.WithMicroOrm().WithQueryLogger(settings.QueryLogger).WithReflectionCache(settings.Cache).AddDubUrl();
                break;
        }
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var configured = provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value;
        var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
        Assert.Multiple(() =>
        {
            Assert.That(configured.DatabaseUrlFactoryType, Is.EqualTo(typeof(MicroOrm.DatabaseUrlFactory)));
            Assert.That(configured.Logger, Is.SameAs(settings.Logger));
            Assert.That(configured.QueryLogger, Is.SameAs(settings.QueryLogger));
            Assert.That(configured.ReflectionCache, Is.SameAs(settings.Cache));
            Assert.That(factory, Is.TypeOf<MicroOrm.DatabaseUrlFactory>());
            Assert.That(factory.QueryLogger, Is.SameAs(settings.QueryLogger));
            Assert.That(((MicroOrm.DatabaseUrlFactory)factory).ReflectionCache, Is.SameAs(settings.Cache));
            Assert.That(provider.GetRequiredService<IQueryLogger>(), Is.SameAs(settings.QueryLogger));
            Assert.That(provider.GetRequiredService<IReflectionCache>(), Is.SameAs(settings.Cache));
            Assert.That(provider.GetRequiredService<IDatabaseUrlFactory>(), Is.SameAs(factory));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DisabledCache_IsAppliedThroughOptionsOrFluentConfiguration(bool fluent)
    {
        var services = new ServiceCollection();
        if (fluent) services.AddDubUrl().WithMicroOrm().WithoutReflectionCache();
        else services.AddDubUrl(new DubUrlServiceOptions().WithMicroOrm().WithoutReflectionCache());
        using var provider = services.BuildServiceProvider();
        var factory = (MicroOrm.DatabaseUrlFactory)provider.GetRequiredService<IDatabaseUrlFactory>();
        Assert.That(factory.ReflectionCache, Is.TypeOf<NoneReflectionCache>());
        Assert.That(factory.ReflectionCache,
            Is.SameAs(provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value.ReflectionCache));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExplicitLogger_ReceivesQueryMessages_AndOverridesHostLogger(bool hostLogging)
    {
        var logger = new RecordingLogger();
        var host = new RecordingProvider();
        var services = new ServiceCollection();
        if (hostLogging) services.AddLogging(builder => builder.AddProvider(host).SetMinimumLevel(LogLevel.Debug));
        services.AddDubUrl(new DubUrlServiceOptions { Logger = logger });
        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IDatabaseUrlFactory>();
        factory.QueryLogger.Log("SELECT 1");
        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<IOptions<DubUrlServiceOptions>>().Value.Logger, Is.SameAs(logger));
            Assert.That(logger.Messages, Is.EqualTo(new[] { (LogLevel.Debug, "SELECT 1") }));
            Assert.That(host.Logger.Messages, Is.Empty);
        });
    }

    [Test]
    public void HostLogging_ProvidesTheDefaultQueryLogger_WithDubUrlCategory()
    {
        var host = new RecordingProvider();
        using var provider = new ServiceCollection().AddDubUrl()
            .AddLogging(builder => builder.AddProvider(host).SetMinimumLevel(LogLevel.Debug))
            .BuildServiceProvider();
        provider.GetRequiredService<IDatabaseUrlFactory>().QueryLogger.Log("SELECT 2");
        Assert.Multiple(() =>
        {
            Assert.That(host.Categories, Does.Contain(DubUrlServiceExtensions.DubUrlLoggerCategory));
            Assert.That(host.Logger.Messages, Is.EqualTo(new[] { (LogLevel.Debug, "SELECT 2") }));
        });
    }

    [Test]
    public void ExplicitQueryLogger_TakesPrecedenceOverMicrosoftLogger()
    {
        var logger = new RecordingLogger();
        var queryLogger = new Mock<IQueryLogger>();
        using var provider = new ServiceCollection()
            .AddDubUrl(options => { options.Logger = logger; options.QueryLogger = queryLogger.Object; })
            .BuildServiceProvider();
        provider.GetRequiredService<IDatabaseUrlFactory>().QueryLogger.Log("SELECT 3");
        queryLogger.Verify(value => value.Log("SELECT 3"), Times.Once);
        Assert.That(logger.Messages, Is.Empty);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OptionsAndFluentHelpers_UseTheSameConfigurationOrder(bool instanceLast)
    {
        var first = Mock.Of<IQueryLogger>();
        var last = Mock.Of<IQueryLogger>();
        var firstCache = Mock.Of<IReflectionCache>();
        var lastCache = Mock.Of<IReflectionCache>();
        var services = new ServiceCollection();
        if (instanceLast)
            services.WithQueryLogger(first).WithReflectionCache(firstCache)
                .AddDubUrl(new DubUrlServiceOptions().WithMicroOrm().WithQueryLogger(last).WithReflectionCache(lastCache));
        else
            services.AddDubUrl(new DubUrlServiceOptions().WithMicroOrm().WithQueryLogger(first).WithReflectionCache(firstCache))
                .WithQueryLogger(last).WithReflectionCache(lastCache);
        using var provider = services.BuildServiceProvider();
        var factory = (MicroOrm.DatabaseUrlFactory)provider.GetRequiredService<IDatabaseUrlFactory>();
        Assert.Multiple(() =>
        {
            Assert.That(factory.QueryLogger, Is.SameAs(last));
            Assert.That(factory.ReflectionCache, Is.SameAs(lastCache));
        });
    }

    [Test]
    public void DefaultCache_IsSharedWithinEachContainer_AndNotAcrossContainers()
    {
        var services = new ServiceCollection().AddDubUrl(options => options.WithMicroOrm());
        using var first = services.BuildServiceProvider();
        using var second = services.BuildServiceProvider();
        var firstFactory = (MicroOrm.DatabaseUrlFactory)first.GetRequiredService<IDatabaseUrlFactory>();
        var secondFactory = (MicroOrm.DatabaseUrlFactory)second.GetRequiredService<IDatabaseUrlFactory>();
        Assert.Multiple(() =>
        {
            Assert.That(firstFactory.ReflectionCache, Is.TypeOf<ReflectionCache>());
            Assert.That(firstFactory.ReflectionCache, Is.SameAs(first.GetRequiredService<IReflectionCache>()));
            Assert.That(secondFactory.ReflectionCache, Is.Not.SameAs(firstFactory.ReflectionCache));
        });
    }

    private static void Apply(DubUrlServiceOptions options, Settings settings)
    {
        options.Logger = settings.Logger;
        options.WithMicroOrm().WithQueryLogger(settings.QueryLogger).WithReflectionCache(settings.Cache);
    }

    public sealed record Settings(IQueryLogger QueryLogger, IReflectionCache Cache, ILogger Logger);

    public sealed class TypedConfiguration(Settings settings) : IConfigureOptions<DubUrlServiceOptions>
    {
        public void Configure(DubUrlServiceOptions options) => Apply(options, settings);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add((logLevel, formatter(state, exception)));
    }

    private sealed class RecordingProvider : ILoggerProvider
    {
        public RecordingLogger Logger { get; } = new();
        public List<string> Categories { get; } = [];
        public ILogger CreateLogger(string categoryName) { Categories.Add(categoryName); return Logger; }
        public void Dispose() { }
    }
}
