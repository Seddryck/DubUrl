using System.Data.Common;
using DubUrl.ProviderTesting;
using IBM.Data.Db2;
using NUnit.Framework;

namespace DubUrl.Provider.Db2.QA;

[TestFixture, Category("Db2")]
public sealed class Db2ProviderTests : ProviderContract
{
    internal static string Url => Environment.GetEnvironmentVariable("DUBURL_DB2_QA_URL")
        ?? throw new InvalidOperationException("Run QA/DubUrl.QA/Db2/deploy-db2-test-env.ps1 first.");
    protected override string ConnectionUrl => Url;
    protected override string CliTableName => "\"Customer\"";
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=@CustId";
    protected override string SelectCustomerByPositionSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=?";
    protected override string SelectAllCustomersSql => "select * from \"Customer\"";
    protected override string SelectYoungestCustomersSql => "select * from \"Customer\" order by \"BirthDate\" desc fetch first $count$ rows only";
    protected override bool SupportsInterval => false;
    protected override string SelectPrimitiveTemplate => "select $value;format=\"value\"$ from SYSIBM.SYSDUMMY1";
    protected override string SelectAliasedPrimitiveTemplate => "select $value;format=\"value\"$ as $columnId;format=\"identity\"$ from SYSIBM.SYSDUMMY1";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | $field; format="identity"$}; separator=", "$ from $table; format="identity"$
        where $clauses:{clause | $clause.Field; format="identity"$ $clause.Operator$ $clause.Value; format="value"$}; separator=" and "$
        """;
    public override void RegisterProviderFactories()
    {
        DbProviderFactories.RegisterFactory("IBM.Data.Db2", DB2Factory.Instance);
        base.RegisterProviderFactories();
    }
    [Test]
    public void RejectsInvalidCredentials()
    {
        using var valid = new ConnectionUrl(Url).Connect();
        var settings = new DB2ConnectionStringBuilder(valid.ConnectionString) { Password = Guid.NewGuid().ToString("N") };
        using var invalid = new DB2Connection(settings.ConnectionString);
        Assert.Throws<DB2Exception>(() => invalid.Open());
    }
    [Test, Category("Schema")]
    public void CreatesAndDiscoversTypedTable()
    {
        using var connection = new ConnectionUrl(Url).Open();
        using var command = connection.CreateCommand();
        var name = "QA_" + Guid.NewGuid().ToString("N").ToUpperInvariant();
        command.CommandText = $"create table {name} (Id integer, Amount decimal(12,3), Name varchar(50), Day date)";
        command.ExecuteNonQuery();
        try
        {
            command.CommandText = $"insert into {name} values (1,17.505,'O''Brien',date '2023-06-10')";
            command.ExecuteNonQuery();
            command.CommandText = $"select Amount,Name,Day,cast(null as varchar(10)) from {name}";
            using (var reader = command.ExecuteReader())
            {
                Assert.That(reader.Read(), Is.True);
                Assert.That(reader.GetDecimal(0), Is.EqualTo(17.505m));
                Assert.That(reader.GetString(1), Is.EqualTo("O'Brien"));
                Assert.That(reader.GetDateTime(2), Is.EqualTo(new DateTime(2023,6,10)));
                Assert.That(reader.IsDBNull(3), Is.True);
            }
            command.CommandText = $"select count(*) from SYSCAT.COLUMNS where TABSCHEMA=current schema and TABNAME='{name}'";
            Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(4));
        }
        finally { command.CommandText = $"drop table {name}"; command.ExecuteNonQuery(); }
    }
}
