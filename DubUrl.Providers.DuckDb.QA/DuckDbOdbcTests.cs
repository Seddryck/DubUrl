using DubUrl.ProviderTesting;
using NUnit.Framework;

namespace DubUrl.Providers.DuckDb.QA;

[TestFixture, Category("DuckDB"), NonParallelizable]
public sealed class DuckDbOdbcTests : OdbcContract
{
    protected override string ConnectionUrl => DuckDbTestDatabase.ConnectionUrl.Replace("duckdb:", "odbc+duckdb:");
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
}
