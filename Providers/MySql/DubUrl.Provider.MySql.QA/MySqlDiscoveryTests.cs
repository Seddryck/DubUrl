#if NET10_0_OR_GREATER
using System.Data;
using DubUrl.Discovery;
using DubUrl.Provider.MySql.Discovery;
using DubUrl.Provider.MySql.Sql;
using DubUrl.Schema;
using DubUrl.Querying.Dialects;
using MySqlConnector;
using NUnit.Framework;

namespace DubUrl.Provider.MySql.QA;

// Catalog integration tests intentionally execute only on .NET 10.
[TestFixture, Category("MySQL"), Category("SchemaDiscovery"), NonParallelizable]
public sealed class MySqlDiscoveryTests
{
    private MySqlConnection connection = null!;
    private string database = "";
    private readonly MySqlMetadataReader reader = new();
    private MetadataName Name(string name) => new(name, Database: database);

    [SetUp]
    public void CreateCatalog()
    {
        connection = new MySqlConnection(Environment.GetEnvironmentVariable("DUBURL_MYSQL_DISCOVERY_QA_CONNECTION_STRING")
            ?? "Server=localhost;Database=DubUrl;User ID=root;Password=Password12!");
        connection.Open();
        database = "discovery_" + Guid.NewGuid().ToString("N");
        Execute($"CREATE DATABASE `{database}`");
        connection.ChangeDatabase(database);
    }

    [TearDown]
    public void DropCatalog()
    {
        try { if (connection?.State == ConnectionState.Open) Execute($"DROP DATABASE IF EXISTS `{database}`"); }
        finally { connection?.Dispose(); }
    }

    [Test]
    public async Task Catalog_PreservesFacetsKeysDefaultsGenerationCommentsIndexesAndViews()
    {
        Execute("""
            CREATE TABLE parent (b INT NOT NULL,a INT NOT NULL,title VARCHAR(40) DEFAULT 'hé' COMMENT 'column comment',
              amount DECIMAL(10,2) DEFAULT 12.25,copy INT GENERATED ALWAYS AS (b) STORED,
              CONSTRAINT pk PRIMARY KEY(b,a),CONSTRAINT uq UNIQUE(title),CONSTRAINT positive CHECK (b>=0)) COMMENT='table comment';
            CREATE TABLE child (x INT,y INT,CONSTRAINT fk FOREIGN KEY(x,y) REFERENCES parent(b,a));
            CREATE INDEX ordered_idx ON parent(a DESC,b ASC);
            CREATE VIEW portable AS SELECT b AS label FROM parent;
            """);
        var metadata = await reader.ReadAsync(connection, new());
        Assert.That(metadata.Diagnostics.Where(d => d.Object?.Database == database), Is.Empty);
        Assert.That(metadata.Coverage.Values, Is.All.EqualTo(MetadataCoverage.Available));
        var table = metadata.Tables.Single(t => t.Identity == Name("parent"));
        Assert.That(table.Columns.Select(c => c.Name), Is.EqualTo(new[] { "b", "a", "title", "amount", "copy" }));
        Assert.That(table.Columns[0].Nullable, Is.False);
        Assert.That(table.Columns[2].Nullable, Is.True);
        Assert.That(table.Columns[2].Length, Is.EqualTo(40));
        Assert.That(table.Columns[2].Default, Is.EqualTo("hé"));
        Assert.That(table.Columns[3].Precision, Is.EqualTo(10));
        Assert.That(table.Columns[3].Scale, Is.EqualTo(2));
        Assert.That(table.Columns[3].Default, Is.EqualTo(12.25m));
        Assert.That(table.Columns[4].Generation!.Expression, Is.EqualTo(new MetadataExpression("column", "b")));
        Assert.That(table.Columns[4].Generation!.Stored, Is.True);
        Assert.That(table.PrimaryKey!.Columns, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(table.UniqueConstraints.Single().Columns, Is.EqualTo(new[] { "title" }));
        Assert.That(table.Comment, Is.EqualTo("table comment"));
        Assert.That(table.Columns[2].Comment, Is.EqualTo("column comment"));
        Assert.That(table.Checks.Single().Operator, Is.EqualTo(">="));
        var index = table.Indexes.Single();
        Assert.That(index.Identity, Is.EqualTo(Name("ordered_idx")));
        Assert.That(index.Columns.Select(c => c.Name), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(index.Columns.Select(c => c.Descending), Is.EqualTo(new[] { true, false }));
        var key = metadata.Tables.Single(t => t.Identity == Name("child")).ForeignKeys.Single();
        Assert.That(key.Target, Is.EqualTo(Name("parent")));
        Assert.That(key.Columns, Is.EqualTo(new[] { "x", "y" }));
        Assert.That(key.TargetColumns, Is.EqualTo(new[] { "b", "a" }));
        Assert.That(metadata.Views.Single().Source, Is.EqualTo(Name("parent")));
        Assert.That(metadata.Views.Single().Projections!.Single().Alias, Is.EqualTo("label"));
        Assert.That(connection.State, Is.EqualTo(ConnectionState.Open));
    }

    [Test]
    public async Task Inspection_FiltersClosesDependenciesAndRoundTripsSupportedTables()
    {
        Execute("CREATE TABLE parent (id INT PRIMARY KEY,title VARCHAR(40),amount DECIMAL(10,2)); CREATE TABLE child (id INT,parent_id INT,FOREIGN KEY(parent_id) REFERENCES parent(id))");
        var options = new DiscoveryOptions { Tables = [Name("child")], Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.PrimaryKeys, SchemaFeature.ForeignKeys] };
        var inspector = new SchemaInspector();
        var missing = await inspector.InspectAsync(connection, reader, options with { AllowPartial = true });
        Assert.That(missing.Diagnostics.Select(d => d.Code), Is.EqualTo(new[] { "SDI005" }));
        Assert.That(missing.Diagnostics.Single().Object, Is.EqualTo(Name("child")));
        var result = await inspector.InspectAsync(connection, reader, options with { IncludeReferencedTables = true });
        Assert.That(result.IsComplete, Is.True, string.Join(",", result.Diagnostics));
        Assert.That(result.Schema!.Tables.Count, Is.EqualTo(2));
        var filtered = await inspector.InspectAsync(connection, reader, options with { Features = [SchemaFeature.Tables, SchemaFeature.Columns] });
        Assert.That(filtered.Schema!.Tables.Values.Single().Name, Is.EqualTo("child"));
        Execute("DROP TABLE child; DROP TABLE parent");
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<MySqlDialect>(["mysql"]);
        var renderer = new SchemaScriptRenderer(dialects.Build().Get<MySqlDialect>());
        Execute(renderer.Render(result.Schema));
        var second = await inspector.InspectAsync(connection, reader, options with { IncludeReferencedTables = true });
        Assert.That(second.IsComplete, Is.True, string.Join(",", second.Diagnostics));
        Assert.That(second.Schema!.Tables.Keys, Is.EqualTo(result.Schema.Tables.Keys));
        foreach (var table in result.Schema.Tables.Values)
            Assert.That(second.Schema.Tables.Values.Single(t => t.Identity == table.Identity).Columns.Values.Select(c => (c.Name, c.Type)),
                Is.EqualTo(table.Columns.Values.Select(c => (c.Name, c.Type))));
    }

    [Test]
    public async Task UnsupportedObjects_PreserveStrictPartialDiagnosticsAndCounts()
    {
        Execute("""
            CREATE TABLE t (id INT PRIMARY KEY AUTO_INCREMENT,title VARCHAR(40),b INT);
            CREATE INDEX expression_idx ON t ((upper(title)));
            CREATE INDEX prefix_idx ON t(title(8));
            CREATE VIEW aggregate_view AS SELECT COUNT(*) AS total FROM t;
            """);
        var options = new DiscoveryOptions { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Indexes, SchemaFeature.Views] };
        var inspector = new SchemaInspector();
        foreach (var partial in new[] { true, false })
        {
            var result = await inspector.InspectAsync(connection, reader, options with { AllowPartial = partial });
            Assert.That(result.IsSuccess, Is.EqualTo(partial));
            Assert.That(result.IsComplete, Is.False);
            Assert.That(result.Diagnostics.Count(d => d.Object?.Database == database), Is.EqualTo(4));
            Assert.That(result.Diagnostics.Count(d => d.Code == "SDI009" && d.Feature == SchemaFeature.Indexes && d.Object == Name("t")), Is.EqualTo(2));
            Assert.That(result.Diagnostics.Count(d => d.Code == "SDI009" && d.Feature == SchemaFeature.Generation && d.Object == Name("t")), Is.EqualTo(1));
            Assert.That(result.Diagnostics.Count(d => d.Code == "SDI008" && d.Feature == SchemaFeature.Views && d.Object == Name("aggregate_view")), Is.EqualTo(1));
            Assert.That(result.Diagnostics.All(d => d.Coverage == MetadataCoverage.Unrepresentable), Is.True);
            Assert.That(result.Schema!.Indexes, Is.Empty);
            Assert.That(result.Schema.Views, Is.Empty);
        }
    }

    [Test]
    public async Task ScalarTypesAndVirtualGeneration_RoundTripWithoutChangingNativeTypes()
    {
        Execute("""
            CREATE TABLE scalars (small TINYINT DEFAULT -2,flag BIT(1),fixed CHAR(12),payload VARBINARY(20),
              fixed_bytes BINARY(8),stamp DATETIME,clock TIME,single_value FLOAT,document JSON,
              large_text TEXT,large_bytes BLOB,copy TINYINT GENERATED ALWAYS AS (small) VIRTUAL);
            """);
        var options = new DiscoveryOptions { Tables = [Name("scalars")], Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.Generation, SchemaFeature.Defaults] };
        var inspector = new SchemaInspector();
        var first = await inspector.InspectAsync(connection, reader, options);
        Assert.That(first.IsComplete, Is.True, string.Join(",", first.Diagnostics));
        var metadata = await reader.ReadAsync(connection, options);
        var columns = metadata.Tables.Single(t => t.Identity == Name("scalars")).Columns;
        Assert.That(columns[0].Type, Is.EqualTo(DbType.SByte));
        Assert.That(columns[0].Default, Is.EqualTo((sbyte)-2));
        Assert.That(columns[^1].Generation!.Stored, Is.False);
        var dialects = new DialectRegistryBuilder();
        dialects.AddDialect<MySqlDialect>(["mysql"]);
        var script = new SchemaScriptRenderer(dialects.Build().Get<MySqlDialect>()).Render(first.Schema!);
        Execute("DROP TABLE scalars");
        Execute(script);
        var after = await reader.ReadAsync(connection, options);
        Assert.That(after.Tables.Single(t => t.Identity == Name("scalars")).Columns.Select(c => (c.Type, c.NativeType, c.Length)),
            Is.EqualTo(columns.Select(c => (c.Type, c.NativeType, c.Length))));
    }

    [Test]
    public async Task UnsupportedUniqueKeysAndDefaults_AreDiagnosedWithoutFabricatingMetadata()
    {
        Execute("""
            CREATE TABLE limitations (title VARCHAR(40),stamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
              CONSTRAINT prefix_key UNIQUE(title(8)));
            CREATE UNIQUE INDEX expression_key ON limitations ((upper(title)));
            """);
        var options = new DiscoveryOptions { Features = [SchemaFeature.Tables, SchemaFeature.Columns, SchemaFeature.UniqueConstraints, SchemaFeature.Defaults] };
        var metadata = await reader.ReadAsync(connection, options);
        var table = metadata.Tables.Single(t => t.Identity == Name("limitations"));
        Assert.That(table.UniqueConstraints, Is.Empty);
        Assert.That(table.Columns.Single(c => c.Name == "stamp").Default, Is.Null);
        var diagnostics = metadata.Diagnostics.Where(d => d.Object == Name("limitations")).ToArray();
        Assert.That(diagnostics.Select(d => d.Feature), Is.EquivalentTo(new[] { SchemaFeature.UniqueConstraints, SchemaFeature.UniqueConstraints, SchemaFeature.Defaults }));
        Assert.That(diagnostics.All(d => d.Code == "SDI009" && d.Coverage == MetadataCoverage.Unrepresentable), Is.True);
    }

    [Test]
    public void PublicProvider_ResolvesDiscoveryCapability()
    {
        System.Data.Common.DbProviderFactories.RegisterFactory("MySqlConnector", MySqlConnectorFactory.Instance);
        var url = new ConnectionUrl("mysql://localhost/" + database);
        Assert.That(url.TryGetCapability<SchemaDiscoveryCapability>(out var capability), Is.True);
        Assert.That(capability!.Reader, Is.TypeOf<MySqlMetadataReader>());
    }

    private void Execute(string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
#endif
