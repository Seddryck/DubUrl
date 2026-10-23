using DubUrl.Querying.Templating;
using System.Data;
using System.Data.Common;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace DubUrl.Adbc.Testing;

[NonParallelizable]
public class DuckdbAdbcTests
{
    [SetUp]
    public void RegisterDriver()
    {
        var filename = OperatingSystem.IsWindows() ? "duckdb.dll"
            : OperatingSystem.IsMacOS() ? "libduckdb.dylib" : "libduckdb.so";
        var path = Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", filename);
        DbProviderFactories.RegisterFactory("DubUrl.Provider.DuckDb.Adbc", AdbcProviderFactory.FromNative(path, "duckdb_adbc_init"));
        DubUrl.Mapping.DefaultRegistry.Refresh();
    }

    [TearDown]
    public void UnregisterDriver() => DbProviderFactories.UnregisterFactory("DubUrl.Provider.DuckDb.Adbc");

    [Test]
    public void ReadScalar_NativeDuckdb_ReturnsResult()
        => Assert.That(new DatabaseUrl("adbc+duckdb://").ReadScalar<long>("SELECT 42::BIGINT"), Is.EqualTo(42));

    [Test]
    public async Task ReadScalarAsync_NativeDuckdb_ReturnsResult()
        => Assert.That(await new DatabaseUrl("adbc+duckdb://").ReadScalarAsync<long>("SELECT 42::BIGINT"), Is.EqualTo(42));

    [Test]
    public void ExecuteReader_NativeDuckdb_PreservesNullsAndEmptyResults()
    {
        var database = new DatabaseUrl("adbc+duckdb://");
        using (var reader = database.ExecuteReader("SELECT NULL::BIGINT AS value"))
        {
            Assert.That(reader.Read(), Is.True);
            Assert.That(reader.IsDBNull(0), Is.True);
            Assert.That(reader.GetValue(0), Is.EqualTo(DBNull.Value));
        }
        Assert.That(database.ReadScalar("SELECT 1 WHERE false"), Is.Null);
    }

    [Test]
    public void Command_NativeDuckdb_BindsParameters()
    {
        using var connection = new ConnectionUrl("adbc+duckdb://").Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(? AS BIGINT) + CAST(? AS BIGINT)";
        var parameter = command.CreateParameter();
        parameter.DbType = DbType.Int64;
        parameter.Value = 41L;
        command.Parameters.Add(parameter);
        var second = command.CreateParameter();
        second.DbType = DbType.Int64;
        second.Value = 1L;
        command.Parameters.Add(second);
        Assert.That(command.ExecuteScalar(), Is.EqualTo(42L));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Reader_HasRowsBeforeRead_DoesNotConsumeFirstRow(bool hasRows)
    {
        using var connection = (DbConnection)new ConnectionUrl("adbc+duckdb://").Open();
        using var command = connection.CreateCommand();
        command.CommandText = hasRows ? "SELECT 42::BIGINT" : "SELECT 42::BIGINT WHERE false";
        using var reader = command.ExecuteReader();
        Assert.That(reader.HasRows, Is.EqualTo(hasRows));
        Assert.That(reader.HasRows, Is.EqualTo(hasRows));
        Assert.That(reader.Read(), Is.EqualTo(hasRows));
        if (hasRows)
            Assert.That(reader.GetInt64(0), Is.EqualTo(42));
        Assert.That(reader.Read(), Is.False);
        Assert.That(reader.HasRows, Is.EqualTo(hasRows));
    }

    [Test]
    public async Task ReadMultipleAsync_NativeDuckdb_StreamsRows()
    {
        var count = 0;
        await foreach (var row in new DatabaseUrl("adbc+duckdb://").ReadMultipleAsync("SELECT i FROM range(3) AS t(i)"))
            count++;
        Assert.That(count, Is.EqualTo(3));
    }

    [Test]
    public void FileDatabase_NativeDuckdb_CanBeReopenedAndDeletedAfterDisposal()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duburl-adbc-{Guid.NewGuid():N}.db");
        var url = $"adbc+duckdb:////{path.Replace('\\', '/')}";
        try
        {
            using (var connection = new ConnectionUrl(url).Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "CREATE TABLE Customer (id BIGINT); INSERT INTO Customer VALUES (42)";
                command.ExecuteNonQuery();
            }
            Assert.That(new DatabaseUrl(url).ReadScalar<long>("SELECT id FROM Customer"), Is.EqualTo(42));
            File.Delete(path);
            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".wal");
        }
    }
    [Test]
    public void BoundTemplate_NativeDuckdb_UsesPositionalParameters()
    {
        var url = new ConnectionUrl("adbc+duckdb://");
        var template = new DubUrl.Querying.Templating.InlineTemplateCommandFactory(url.Dialect, url.Parametrizer)
            .Create("SELECT CAST($first; format=\"parameter\"$ AS BIGINT) + CAST($second; format=\"parameter\"$ AS BIGINT)");
        var rendered = template.RenderCommand(new Dictionary<string, object?> { ["first"] = 41L, ["second"] = 1L });
        Assert.That(new DatabaseUrl(url.Url).ReadScalar<long>(rendered), Is.EqualTo(42L));
    }
}
