using DubUrl.Conversion;
using DubUrl.Mapping;
using DubUrl.Parsing;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Parametrizing;
using Moq;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Core.Testing.Conversion;

public static class ConversionTestSupport
{
    public static IDialect Dialect => Mock.Of<IDialect>();
    public static IParametrizer Parametrizer => Mock.Of<IParametrizer>();

    public static SchemeRegistry Registry(string scheme, DbProviderFactory factory,
        Func<DbConnectionStringBuilder, IMapper> mapper)
        => new ProviderCatalogBuilder().AddModule(new Module(scheme, factory, mapper)).Build();

    public static ConnectionUri RoundTrip(string scheme, string input, DbProviderFactory factory,
        Func<DbConnectionStringBuilder, IMapper> mapper)
    {
        var registry = Registry(scheme, factory, mapper);
        var uri = new ConnectionStringConverter(registry).Convert(scheme, input);
        var expected = factory.CreateConnectionStringBuilder()!;
        expected.ConnectionString = input;
        var actual = factory.CreateConnectionStringBuilder()!;
        actual.ConnectionString = new ConnectionUrl(uri.ToString(false), registry).Parse();
        foreach (var key in expected.Keys.Cast<string>().Concat(actual.Keys.Cast<string>()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            expected.TryGetValue(key, out var first);
            actual.TryGetValue(key, out var second);
            Assert.That(System.Convert.ToString(second), Is.EqualTo(System.Convert.ToString(first)), key);
        }
        return uri;
    }

    private sealed class Module(string scheme, DbProviderFactory factory,
        Func<DbConnectionStringBuilder, IMapper> mapper) : IDubUrlProviderModule
    {
        public void Register(IProviderCatalogBuilder catalog)
            => catalog.AddProvider(new ProviderRegistration("Test provider", [scheme], "Test.Provider",
                () => factory, Dialect, () => Parametrizer, context => mapper(context.ConnectionStringBuilder)));
    }
}
