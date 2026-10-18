using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Providers.Sqlite.QA;

public class ConnectionSourceTests
{
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
