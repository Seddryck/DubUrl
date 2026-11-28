using System.Data;
using System.Data.Common;
using DubUrl.Mapping;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using NUnit.Framework;

namespace DubUrl.Core.Testing;

[NonParallelizable]
public class ConnectionUrlBatchLiveTests
{
    [TestCase("DUBURL_SQLSERVER_BATCH_QA_URL", typeof(SqlBatch), TestName = "CreateBatch_SQLServer_Live")]
    [TestCase("DUBURL_POSTGRESQL_BATCH_QA_URL", typeof(NpgsqlBatch), TestName = "CreateBatch_PostgreSQL_Live")]
    [TestCase("DUBURL_MYSQL_BATCH_QA_URL", typeof(MySqlBatch), TestName = "CreateBatch_MySQL_Live")]
    public async Task CreateBatch_ExecutesParameterizedCommandsInOrder(string urlVariable, Type expectedBatchType)
    {
        var url = Environment.GetEnvironmentVariable(urlVariable);
        if (string.IsNullOrWhiteSpace(url))
            Assert.Ignore($"Set {urlVariable} to a live database URL to run this test.");

        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);
        DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnectorFactory.Instance);
        var registry = new ProviderCatalogBuilder()
            .AddModule(new AttributeProviderModule(new AssemblyTypesProbe().Assemblies))
            .Build();

        var batch = new ConnectionUrl(url!, registry).CreateBatch();
        await using var connection = batch.Connection;
        await using (batch)
        {
            Assert.That(connection?.State, Is.EqualTo(ConnectionState.Open));
            Assert.That(batch, Is.TypeOf(expectedBatchType));

            int[] expectedValues = [41, 73];
            foreach (var value in expectedValues)
            {
                var command = batch.CreateBatchCommand();
                command.CommandText = "SELECT @value";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "value";
                parameter.DbType = DbType.Int32;
                parameter.Value = value;
                command.Parameters.Add(parameter);
                batch.BatchCommands.Add(command);
            }

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using var reader = await batch.ExecuteReaderAsync(timeout.Token);
            var results = new List<int>();
            do
            {
                Assert.That(await reader.ReadAsync(timeout.Token), Is.True);
                results.Add(reader.GetInt32(0));
                Assert.That(await reader.ReadAsync(timeout.Token), Is.False);
            }
            while (await reader.NextResultAsync(timeout.Token));

            Assert.That(results, Is.EqualTo(expectedValues));
        }
    }
}
