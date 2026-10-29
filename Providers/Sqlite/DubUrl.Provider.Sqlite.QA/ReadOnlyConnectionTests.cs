using System.Data.Common;
using DubUrl.Mapping;
using DubUrl.Provider.Sqlite.Mapping;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace DubUrl.Provider.Sqlite.QA;

[NonParallelizable]
public class ReadOnlyConnectionTests
{
    private string path = null!;
    private ISchemeRegistry registry = null!;
    private DbProviderFactory? previous;
    private bool hadFactory;
    private string Url => "sqlite:///" + path.Replace('\\', '/');

    [SetUp]
    public void Setup()
    {
        SQLitePCL.Batteries_V2.Init();
        hadFactory = DbProviderFactories.TryGetFactory("Microsoft.Data.Sqlite", out previous);
        DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", SqliteFactory.Instance);
        var builder = new ProviderCatalogBuilder();
        builder.AddModule(new AttributeProviderModule(typeof(SqliteMapper).Assembly));
        registry = builder.Build();
        path = Path.Combine(Path.GetTempPath(), "duburl-readonly-" + Guid.NewGuid().ToString("N") + ".db");
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ConnectionString);
        connection.Open();
        Execute(connection, "CREATE TABLE sample(value INTEGER); INSERT INTO sample VALUES (42)");
    }

    [TearDown]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".attached")) File.Delete(path + ".attached");
        if (hadFactory) DbProviderFactories.RegisterFactory("Microsoft.Data.Sqlite", previous!);
        else DbProviderFactories.UnregisterFactory("Microsoft.Data.Sqlite");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OpenCloseReopenAndPoolReuse_ReadsSucceedWritesFail(bool pooling)
    {
        var target = new ConnectionUrl(Url + "?Mode=ReadWriteCreate", registry).WithReadOnly()
            .WithPooling(new() { Enabled = pooling }).WithOptional(options => options.WithApplicationName("Ignored"));
        for (var lease = 0; lease < 2; lease++)
        {
            using var connection = (SqliteConnection)target.Open();
            AssertReadOnly(connection);
            connection.Close();
            connection.Open();
            AssertReadOnly(connection);
        }
        Assert.That(target.SkippedOptions.Count, Is.EqualTo(1));
    }

    private static void AssertReadOnly(SqliteConnection connection)
    {
        using var query = connection.CreateCommand();
        query.CommandText = "SELECT value FROM sample";
        Assert.That(query.ExecuteScalar(), Is.EqualTo(42L));
        Assert.That(Assert.Throws<SqliteException>(() => Execute(connection, "INSERT INTO sample VALUES (43)"))!.SqliteErrorCode, Is.EqualTo(8));
    }

    [TestCase(false, "INSERT INTO sample VALUES (43)")]
    [TestCase(true, "INSERT INTO sample VALUES (43)")]
    [TestCase(false, "CREATE TABLE forbidden(value INTEGER)")]
    [TestCase(true, "DROP TABLE sample")]
    public void PersistentDmlAndDdl_AreRejectedIncludingExplicitTransactions(bool transaction, string sql)
    {
        using var connection = (SqliteConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        if (transaction) Execute(connection, "BEGIN");
        Assert.That(Assert.Throws<SqliteException>(() => Execute(connection, sql))!.SqliteErrorCode, Is.EqualTo(8));
        if (transaction) Execute(connection, "ROLLBACK");
    }

    [Test]
    public void MissingFile_IsNotCreated()
    {
        File.Delete(path);
        Assert.Throws<SqliteException>(() => new ConnectionUrl(Url, registry).WithReadOnly().Open());
        Assert.That(File.Exists(path), Is.False);
    }

    [Test]
    public void WritableConnections_DoNotInheritReadOnlyPoolState()
    {
        using var reader = (SqliteConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        using var writer = (SqliteConnection)new ConnectionUrl(Url, registry).Open();
        Execute(writer, "INSERT INTO sample VALUES (99)");
        Assert.That(Assert.Throws<SqliteException>(() => Execute(reader, "INSERT INTO sample VALUES (100)"))!.SqliteErrorCode, Is.EqualTo(8));
        using var query = writer.CreateCommand();
        query.CommandText = "SELECT count(*) FROM sample";
        Assert.That(query.ExecuteScalar(), Is.EqualTo(2L));
    }

    [Test]
    public void TemporaryObjects_FollowNativeRulesAndAreWritable()
    {
        using var connection = (SqliteConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        Execute(connection, "CREATE TEMP TABLE scratch(value INTEGER); INSERT INTO scratch VALUES (1)");
        AssertReadOnly(connection);
    }

    [Test]
    public void OrdinaryAttachedFiles_InheritNativeReadOnlyOpenFlags()
    {
        var attached = path + ".attached";
        using (var setup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = attached, Pooling = false }.ConnectionString))
        {
            setup.Open();
            Execute(setup, "CREATE TABLE sample(value INTEGER)");
        }
        using var connection = (SqliteConnection)new ConnectionUrl(Url, registry).WithReadOnly().Open();
        using var attach = connection.CreateCommand();
        attach.CommandText = "ATTACH DATABASE $path AS extra";
        attach.Parameters.AddWithValue("$path", attached);
        attach.ExecuteNonQuery();
        Assert.That(Assert.Throws<SqliteException>(() => Execute(connection, "INSERT INTO extra.sample VALUES (1)"))!.SqliteErrorCode, Is.EqualTo(8));
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
