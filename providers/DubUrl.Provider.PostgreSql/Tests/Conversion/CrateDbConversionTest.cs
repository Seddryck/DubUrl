using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.PostgreSql;

public class CrateDbConversionTest
{
    [TestCase(@"Host=localhost;Database=db;Username=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("cratedb", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new CrateDbMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
