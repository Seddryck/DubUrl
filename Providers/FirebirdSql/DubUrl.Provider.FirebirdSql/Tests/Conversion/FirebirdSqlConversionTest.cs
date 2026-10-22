using DubUrl.Provider.FirebirdSql.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.FirebirdSql.Tests.Conversion;

public class FirebirdSqlConversionTest
{
    [TestCase(@"DataSource=localhost;Database=example.fdb;User=example;Password=example;Port=3051")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("firebird", input, global::FirebirdSql.Data.FirebirdClient.FirebirdClientFactory.Instance,
            csb => new FirebirdSqlMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
