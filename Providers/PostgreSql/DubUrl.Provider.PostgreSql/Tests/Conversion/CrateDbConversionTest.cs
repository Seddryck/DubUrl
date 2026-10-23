using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.PostgreSql.Tests.Conversion;

public class CrateDbConversionTest
{
    [TestCase(@"Host=localhost;Database=db;Username=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("cratedb", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new CrateDbMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
