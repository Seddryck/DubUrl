using DubUrl.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System.Data.Common;

namespace DubUrl.Provider.Sqlite.QA;

[NonParallelizable]
public class OptionalConfigurationTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void OpenAndReopen_KeepSupportedPoolingAndReportUnsupportedApplicationName(bool pooling)
    {
        SQLitePCL.Batteries_V2.Init();
        const string invariant = "Microsoft.Data.Sqlite";
        var hadFactory = DbProviderFactories.TryGetFactory(invariant, out var previous);
        DbProviderFactories.RegisterFactory(invariant, SqliteFactory.Instance);
        try
        {
            var builder = new ProviderCatalogBuilder();
            builder.AddModule(new AttributeProviderModule(typeof(SqliteMapper).Assembly));
            var target = new ConnectionUrl("sqlite://", builder.Build()).WithOptional(options => options
                .WithApplicationName("Reporting").WithPooling(new() { Enabled = pooling }));
            for (var attempt = 0; attempt < 2; attempt++)
            {
                using var connection = (SqliteConnection)target.Open();
                Assert.That(new SqliteConnectionStringBuilder(connection.ConnectionString).Pooling, Is.EqualTo(pooling));
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT 42";
                Assert.That(command.ExecuteScalar(), Is.EqualTo(42L));
                connection.Close();
                connection.Open();
                Assert.That(command.ExecuteScalar(), Is.EqualTo(42L));
            }
            Assert.That(target.SkippedOptions.Single().Option, Is.EqualTo(ConnectionConfigurationOption.ApplicationName));
        }
        finally
        {
            if (hadFactory) DbProviderFactories.RegisterFactory(invariant, previous!);
            else DbProviderFactories.UnregisterFactory(invariant);
        }
    }
}
