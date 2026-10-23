using DubUrl.Provider.DuckDb.Mapping;
using DubUrl.Core.Testing.Conversion;
using Moq;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Provider.DuckDb.Tests.Conversion;

public class DuckdbAdbcConversionTest
{
    [TestCase("path=:memory:")]
    [TestCase("path=example.duckdb;access_mode=read_only")]
    [TestCase("path=example db.duckdb")]
    public void Convert_PreservesAdbcPathAndOptions(string input)
    {
        var factory = new Mock<DbProviderFactory>();
        factory.Setup(x => x.CreateConnectionStringBuilder()).Returns(() => new DbConnectionStringBuilder());
        ConversionTestSupport.RoundTrip("adbc+duckdb", input, factory.Object,
            csb => new DuckdbAdbcMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer, string.Empty));
    }
}
