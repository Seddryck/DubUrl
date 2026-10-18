using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.PostgreSql;

public class QuestDbConversionTest
{
    [TestCase(@"Host=localhost;Port=8812;Database=qdb;Username=example;Password=example;ServerCompatibilityMode=NoTypeLoading")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("questdb", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new QuestDbMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
