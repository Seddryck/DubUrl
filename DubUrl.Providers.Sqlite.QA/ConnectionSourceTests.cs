using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Providers.Sqlite.QA;

public class ConnectionSourceTests
{
    [TestCase(ConnectionReuseMode.ProviderManaged)]
    [TestCase(ConnectionReuseMode.RetainedExclusive)]
    public async Task DatabaseQueriesAndReadersUseLeases(ConnectionReuseMode mode)
    {
        SQLitePCL.Batteries_V2.Init();
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
        await using var source = new ConnectionSourceUrl(SqliteTestDatabase.ConnectionUrl).Create(new() { ReuseMode = mode });
        await using (var database = new DatabaseUrl(source))
        {
            Assert.That(database.ReadScalar<long>("SELECT COUNT(*) FROM Customer"), Is.EqualTo(5));
            Assert.That(await database.ReadScalarAsync<long>("SELECT COUNT(*) FROM Customer"), Is.EqualTo(5));
            Assert.That(database.ReadFirst("SELECT CustomerId FROM Customer"), Is.Not.Null);
            Assert.That(database.ReadSingle("SELECT CustomerId FROM Customer WHERE CustomerId = 1"), Is.Not.Null);
            Assert.That(database.ReadMultiple("SELECT CustomerId FROM Customer").Take(1).ToList(), Has.Count.EqualTo(1));
            await using var reader = await database.ExecuteReaderAsync("SELECT CustomerId FROM Customer");
            Assert.That(await reader.ReadAsync(), Is.True);
        }
        // The database borrowed the source: another borrower can still query it.
        await using var microOrm = new DubUrl.MicroOrm.DatabaseUrl(source);
        Assert.That(microOrm.ReadMultiple<CustomerIdRow>("SELECT CustomerId FROM Customer").ToList(), Has.Count.EqualTo(5));
        await foreach (var _ in microOrm.ReadMultipleAsync<CustomerIdRow>("SELECT CustomerId FROM Customer")) break;
        Assert.That(await microOrm.ReadFirstAsync<CustomerIdRow>("SELECT CustomerId FROM Customer ORDER BY CustomerId"), Is.Not.Null);
    }

    public sealed class CustomerIdRow { public long CustomerId { get; set; } }

    [TestCase(ConnectionReuseMode.ProviderManaged)]
    [TestCase(ConnectionReuseMode.RetainedExclusive)]
    public async Task LeasesSupportCommandsAndTransactions(ConnectionReuseMode mode)
    {
        SQLitePCL.Batteries_V2.Init();
        System.Data.Common.DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
        await using var source = new ConnectionSourceUrl(SqliteTestDatabase.ConnectionUrl).Create(new() { ReuseMode = mode });
        for (var i = 0; i < 2; i++)
        {
            await using var lease = await source.AcquireAsync();
            await using var transaction = await lease.Connection.BeginTransactionAsync();
            await using var command = lease.Connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT COUNT(*) FROM Customer";
            Assert.That(Convert.ToInt32(await command.ExecuteScalarAsync()), Is.EqualTo(5));
            await transaction.CommitAsync();
        }
    }
}
