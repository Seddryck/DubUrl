using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.PostgreSql.Tests.Conversion;

public class TimescaleConversionTest
{
    [TestCase(@"Host=localhost;Database=db;Username=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("timescale", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new TimescaleMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
