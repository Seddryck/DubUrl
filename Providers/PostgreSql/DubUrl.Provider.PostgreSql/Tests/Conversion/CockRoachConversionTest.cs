using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.PostgreSql.Tests.Conversion;

public class CockRoachConversionTest
{
    [TestCase(@"Host=localhost;Port=26257;Database=db;Username=example")]
    [TestCase(@"Host=localhost;Database=db;Username=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("cockroach", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new CockRoachMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
