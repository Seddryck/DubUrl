using System.Data.Common;
using DubUrl.ProviderTesting;
using NUnit.Framework;
using Oracle.ManagedDataAccess.Client;

namespace DubUrl.Provider.Oracle.QA;

[TestFixture, Category("Oracle")]
public sealed class OracleProviderTests : ProviderContract
{
    protected override string ConnectionUrl => OracleTestDatabase.ConnectionUrl;
    protected override string SelectFirstCustomerSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=1";
    protected override string SelectCustomerByIdSql => "select \"FullName\" from \"Customer\" where \"CustomerId\"=:CustId";
    protected override string SelectCustomerByPositionSql => SelectCustomerByIdSql;
    protected override bool SupportsPositionalParameters => false;
    // Oracle has no standalone TIME type; the ANSI interval formatter is not Oracle syntax.
    protected override bool SupportsTime => false;
    protected override bool SupportsInterval => false;
    protected override string SelectPrimitiveTemplate => "select $value; format=\"value\"$ from dual";
    protected override string SelectAliasedPrimitiveTemplate => "select $value; format=\"value\"$ AS $columnId;format=\"identity\"$ from dual";
    protected override string SelectAllCustomersSql => "select * from \"Customer\"";
    protected override string SelectYoungestCustomersSql => "select * from \"Customer\" order by \"BirthDate\" desc fetch first $count$ rows only";
    protected override string SelectWhereCustomersTemplate => """
        select $fields:{field | $field; format="identity"$}; separator=", "$ from $table; format="identity"$
        where $clauses:{clause | $clause.Field; format="identity"$ $clause.Operator$ $clause.Value; format="value"$}; separator=" and "$
        """;

    public override void RegisterProviderFactories()
    {
        DbProviderFactories.RegisterFactory("Oracle.ManagedDataAccess", OracleClientFactory.Instance);
        base.RegisterProviderFactories();
    }

    [Test]
    public void TemplateBindsRepeatedValuesByName()
    {
        var database = new DatabaseUrl(ConnectionUrl);
        var actual = database.ReadScalarNonNull<decimal>(
            "select $second;format=\"value\"$ - $first;format=\"value\"$ + $second;format=\"value\"$ from dual",
            new Dictionary<string, object?> { ["first"] = 3, ["second"] = 11 });
        Assert.That(actual, Is.EqualTo(19m));
    }

    [Test]
    public void NamedParametersBindIndependentlyOfCollectionOrder()
    {
        using var connection = OracleTestDatabase.Open();
        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "select :second - :first + :second from dual";
        command.Parameters.Add("first", OracleDbType.Int32).Value = 3;
        command.Parameters.Add("second", OracleDbType.Int32).Value = 11;
        Assert.That(Convert.ToDecimal(command.ExecuteScalar()), Is.EqualTo(19m));
    }

    [Test]
    public void RoundTripsOracleTypesAndNulls()
    {
        using var connection = OracleTestDatabase.Open();
        using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandText = "select cast(:amount as number(18,3)), cast(:text as varchar2(100)), cast(:day as date), cast(:stamp as timestamp), cast(:missing as varchar2(100)), cast(:empty as varchar2(100)) from dual";
        var stamp = new DateTime(2023, 6, 10, 17, 52, 12);
        command.Parameters.Add("amount", OracleDbType.Decimal).Value = 17.505m;
        command.Parameters.Add("text", OracleDbType.Varchar2).Value = "O'Brien";
        command.Parameters.Add("day", OracleDbType.Date).Value = stamp.Date;
        command.Parameters.Add("stamp", OracleDbType.TimeStamp).Value = stamp;
        command.Parameters.Add("missing", OracleDbType.Varchar2).Value = DBNull.Value;
        command.Parameters.Add("empty", OracleDbType.Varchar2).Value = "";
        using var reader = command.ExecuteReader();
        Assert.That(reader.Read(), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(reader.GetDecimal(0), Is.EqualTo(17.505m));
            Assert.That(reader.GetString(1), Is.EqualTo("O'Brien"));
            Assert.That(reader.GetDateTime(2), Is.EqualTo(stamp.Date));
            Assert.That(reader.GetDateTime(3), Is.EqualTo(stamp));
            Assert.That(reader.IsDBNull(4), Is.True);
            Assert.That(reader.IsDBNull(5), Is.True, "Oracle treats empty strings as NULL.");
        });
    }

    [Test]
    public void RejectsInvalidCredentials()
    {
        using var connection = OracleTestDatabase.Open();
        var settings = new OracleConnectionStringBuilder(connection.ConnectionString) { Password = Guid.NewGuid().ToString("N"), Pooling = false };
        using var invalid = new OracleConnection(settings.ConnectionString);
        var exception = Assert.Throws<OracleException>(() => invalid.Open());
        Assert.That(exception!.Number, Is.EqualTo(1017));
    }
}
