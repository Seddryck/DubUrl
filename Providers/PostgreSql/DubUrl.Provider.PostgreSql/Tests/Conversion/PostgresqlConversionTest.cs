using DubUrl.Provider.PostgreSql.Mapping;
using DubUrl.Core.Testing;
using DubUrl.Core.Testing.Conversion;
using NUnit.Framework;

namespace DubUrl.Provider.PostgreSql.Tests.Conversion;

public class PostgresqlConversionTest
{
    [TestCase(@"Host=localhost;Database=db;Username=example;Password=""p;a@ss+%&"";Timeout=5")]
    [TestCase(@"Host=::1;Port=5433;Database=""db /é"";Username=example")]
    [TestCase(@"Host=host1,host2;Database=db;Username=example;SSL Mode=Require")]
    public void Convert_PreservesProviderSettings(string input)
    {
        ConversionTestSupport.RoundTrip("postgresql", input, global::Npgsql.NpgsqlFactory.Instance,
            csb => new PostgresqlMapper(csb, ConversionTestSupport.Dialect, ConversionTestSupport.Parametrizer));
    }
}
