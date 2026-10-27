using System.Data.Common;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Querying.Dialects;
using DubUrl.Schema.Testing.Contracts;
using NUnit.Framework;

namespace DubUrl.Provider.SingleStore.QA;

[Category("SingleStore")]
public sealed class SingleStoreSchemaTests : SchemaContract
{
    protected override DbConnection CreateConnection() => SingleStoreTestDatabase.CreateConnection();
    protected override IDialect CreateDialect()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<SingleStoreDialect>(["singlestore"]);
        return builder.Build().Get<SingleStoreDialect>();
    }
    protected override SchemaCapabilities Capabilities => SchemaCapabilities.CreateTable | SchemaCapabilities.DropTableIfExists;
    protected override string QuoteIdentifier(string identifier) => $"`{identifier}`";

    [Test]
    public void AutoIncrementPromotesIntegerColumnToBigInt()
    {
        var tableName = "QaIdentity_" + Guid.NewGuid().ToString("N");
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"create table `{tableName}` (Id int not null auto_increment primary key)";
        command.ExecuteNonQuery();
        try
        {
            command.CommandText = "select data_type from information_schema.columns where table_schema=database() and table_name=@name and column_name='Id'";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            Assert.That(command.ExecuteScalar(), Is.EqualTo("bigint"));
            command.Parameters.Clear();
            command.CommandText = $"insert into `{tableName}` () values ()";
            command.ExecuteNonQuery();
            command.CommandText = $"select Id from `{tableName}`";
            Assert.That(command.ExecuteScalar(), Is.TypeOf<long>().And.GreaterThan(0));
        }
        finally
        {
            command.Parameters.Clear();
            command.CommandText = $"drop table if exists `{tableName}`";
            command.ExecuteNonQuery();
        }
    }

    [Test]
    public void DiscoversFixtureColumnsAndTwoPartitionDatabase()
    {
        using var connection = CreateConnection();
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select column_name, data_type from information_schema.columns where table_schema=database() and table_name='Customer' order by ordinal_position";
        using (var reader = command.ExecuteReader())
        {
            var columns = new List<(string, string)>();
            while (reader.Read()) columns.Add((reader.GetString(0), reader.GetString(1)));
            Assert.That(columns, Is.EqualTo(new[] { ("CustomerId", "int"), ("FullName", "varchar"), ("BirthDate", "date") }));
        }
        command.CommandText = "select num_partitions from information_schema.distributed_databases where database_name=database()";
        Assert.That(Convert.ToInt32(command.ExecuteScalar()), Is.EqualTo(2));
    }
}
