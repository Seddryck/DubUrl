using System.Data.Common;
using DuckDB.NET.Data;
using DubUrl.Mapping;
using DubUrl.Provider.DuckDb.Mapping;
using NUnit.Framework;

namespace DubUrl.Provider.DuckDb.QA;

[NonParallelizable]
public class ReadOnlyConnectionTests
{
    private string path = null!;
    private ISchemeRegistry registry = null!;
    private DbProviderFactory? previous;
    private bool hadFactory;
    private string Url => "duckdb:///" + path.Replace('\\', '/');

    [SetUp]
    public void Setup()
    {
        hadFactory = DbProviderFactories.TryGetFactory("DuckDB.NET.Data", out previous);
        DbProviderFactories.RegisterFactory("DuckDB.NET.Data", DuckDBClientFactory.Instance);
        var builder = new ProviderCatalogBuilder();
        builder.AddModule(new AttributeProviderModule(typeof(DuckdbMapper).Assembly));
        registry = builder.Build();
        path = Path.Combine(Path.GetTempPath(), "duburl-readonly-" + Guid.NewGuid().ToString("N") + ".duckdb");
        using var setup = new DuckDBConnection(new DuckDBConnectionStringBuilder { DataSource = path }.ConnectionString);
        setup.Open();
        Execute(setup, "CREATE TABLE sample(value INTEGER); INSERT INTO sample VALUES (42)");
    }

    [TearDown]
    public void Cleanup()
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".attached")) File.Delete(path + ".attached");
        if (hadFactory) DbProviderFactories.RegisterFactory("DuckDB.NET.Data", previous!);
        else DbProviderFactories.UnregisterFactory("DuckDB.NET.Data");
    }

    [Test]
    public void OpenAndReopen_NeverDowngradeTheRequestedReadOnlyMode()
    {
        var target = new ConnectionUrl(Url + "?access_mode=READ_WRITE", registry).WithReadOnly()
            .WithOptional(options => options.WithApplicationName("Ignored"));
        for (var lease = 0; lease < 2; lease++)
        {
            using var connection = (DuckDBConnection)target.Open();
            AssertReadOnly(connection);
            connection.Close();
            connection.Open();
            AssertReadOnly(connection);
        }
    }

    private static void AssertReadOnly(DbConnection connection)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT value FROM sample";
        Assert.That(Convert.ToInt32(query.ExecuteScalar()), Is.EqualTo(42));
        Assert.Throws<DuckDBException>(() => Execute(connection, "INSERT INTO sample VALUES (43)"));
    }

    [TestCase(false, "INSERT INTO sample VALUES (43)")]
    [TestCase(true, "INSERT INTO sample VALUES (43)")]
    [TestCase(false, "CREATE TABLE forbidden(value INTEGER)")]
    [TestCase(true, "DROP TABLE sample")]
    public void PersistentDmlAndDdl_AreRejectedIncludingExplicitTransactions(bool transaction, string sql)
    {
        using var connection = new ConnectionUrl(Url, registry).WithReadOnly().Open();
        if (transaction) Execute((DbConnection)connection, "BEGIN");
        Assert.Throws<DuckDBException>(() => Execute((DbConnection)connection, sql));
        if (transaction) Execute((DbConnection)connection, "ROLLBACK");
    }

    [Test]
    public void MissingFile_IsNotCreated()
    {
        File.Delete(path);
        Assert.Throws<DuckDBException>(() => new ConnectionUrl(Url, registry).WithReadOnly().Open());
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void ExistingWritableInstance_CannotBeSilentlyReusedForReadOnly()
    {
        using var writer = (DuckDBConnection)new ConnectionUrl(Url, registry).Open();
        Assert.Throws<InvalidOperationException>(() => new ConnectionUrl(Url, registry).WithReadOnly().Open());
        Execute(writer, "INSERT INTO sample VALUES (99)");
    }

    [Test]
    public void DirectNativeOpenAndReopen_AreAlsoGuardedAndCloseOnFailure()
    {
        using var reader = (DuckDBConnection)new ConnectionUrl(Url, registry).WithReadOnly().Connect();
        reader.Open();
        reader.Close();
        using var writer = (DuckDBConnection)new ConnectionUrl(Url, registry).Open();
        Assert.Throws<InvalidOperationException>(() => reader.Open());
        Assert.That(reader.State, Is.EqualTo(System.Data.ConnectionState.Closed));
        Execute(writer, "INSERT INTO sample VALUES (99)");
    }

    [Test]
    public void ExplicitReadOnlyAttachment_RejectsWritesToThatDatabase()
    {
        var attached = path + ".attached";
        using (var setup = new DuckDBConnection(new DuckDBConnectionStringBuilder { DataSource = attached }.ConnectionString))
        {
            setup.Open();
            Execute(setup, "CREATE TABLE sample(value INTEGER)");
        }
        using var reader = (DuckDBConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        Execute(reader, "ATTACH '" + attached.Replace("'", "''") + "' AS extra (READ_ONLY)");
        Assert.Throws<DuckDBException>(() => Execute(reader, "INSERT INTO extra.sample VALUES (1)"));
        AssertReadOnly(reader);
    }

    [Test]
    public void ReadOnlyThenWritable_LeavesNoModeStateBehind()
    {
        using (var reader = (DuckDBConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open()) AssertReadOnly(reader);
        using var writer = (DuckDBConnection)new ConnectionUrl(Url, registry).Open();
        Execute(writer, "INSERT INTO sample VALUES (99)");
    }

    [Test]
    public void TemporaryObjects_FollowNativeRulesAndAreWritable()
    {
        using var connection = (DuckDBConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        Execute(connection, "CREATE TEMP TABLE scratch(value INTEGER); INSERT INTO scratch VALUES (1)");
        AssertReadOnly(connection);
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
