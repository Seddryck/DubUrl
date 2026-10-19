using DubUrl.Mapping.Implementation;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Testing.Conversion.Snowflake;

public class SnowflakeConversionTest
{
    [TestCase(@"ACCOUNT=example;DB=db;SCHEMA=public;USER=example;PASSWORD=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("snowflake", input, global::Snowflake.Data.Client.SnowflakeDbFactory.Instance,
            csb => new SnowflakeMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
