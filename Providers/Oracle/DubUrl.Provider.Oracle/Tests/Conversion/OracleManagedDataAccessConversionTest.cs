using DubUrl.Provider.Oracle.Mapping;
using DubUrl.Testing;
using DubUrl.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.Oracle.Tests.Conversion;

public class OracleManagedDataAccessConversionTest
{
    [TestCase(@"Data Source=localhost;User ID=example;Password=example")]
    [TestCase(@"Data Source=""(DESCRIPTION=(ADDRESS=(PROTOCOL=tcp)(HOST=localhost)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=db)))"";User ID=example;Password=example")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("oracle", input, global::Oracle.ManagedDataAccess.Client.OracleClientFactory.Instance,
            csb => new OracleManagedDataAccessMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
