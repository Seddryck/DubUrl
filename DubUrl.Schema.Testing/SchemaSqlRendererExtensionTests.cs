using System.Data;
using DubUrl.Querying.Dialects;
using DubUrl.Querying.Dialects.Casters;
using DubUrl.Querying.Dialects.Functions;
using DubUrl.Querying.Dialects.Renderers;
using DubUrl.Querying.TypeMapping;
using DubUrl.Schema.Constraints;
using NUnit.Framework;
using ForeignKey = DubUrl.Schema.Constraints.ForeignKeyConstraint;

namespace DubUrl.Schema.Testing;

public class SchemaSqlRendererExtensionTests
{
    private IDialect source = null!;

    [SetUp]
    public void SetUp()
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<TSqlDialect>(["mssql"]);
        source = builder.Build().Get<TSqlDialect>();
    }

    [Test]
    public void IdentityOverride_InheritsObjectQuotingAndGeneratedColumnSyntax()
    {
        var dialect = new ProviderDialect(source, new IdentityRenderer());
        var table = new Table("Order", [
            new Column("Id", DbType.Int64, generation: new IdentityColumnGeneration(100, 5)),
            new Column("Computed", DbType.Int32, generation: new GeneratedColumnGeneration(new ValueExpression(7)))
        ]);

        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([table], []));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("CREATE TABLE [Order]"));
            Assert.That(sql, Does.Contain("[Id] BIGINT PROVIDER_IDENTITY(100, 5)"));
            Assert.That(sql, Does.Contain("[Computed] AS (7) PERSISTED"));
        });
    }

    [TestCase(GeneratedColumnStorage.Stored, "True")]
    [TestCase(GeneratedColumnStorage.Virtual, "False")]
    public void GeneratedOverride_ReceivesRenderedExpressionAndStorage(GeneratedColumnStorage storage, string expected)
    {
        var dialect = new ProviderDialect(source, new GeneratedRenderer());
        var table = new Table("Order", [new Column("Computed", DbType.String,
            generation: new GeneratedColumnGeneration(new ValueExpression("owner's"), storage))]);

        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([table], []));

        Assert.That(sql, Does.Contain($"[Computed] NVARCHAR PROVIDER_GENERATED('owner''s', {expected})"));
    }

    [Test]
    public void RegexOverride_UsesEscapedInputsAndSharedNullGuard()
    {
        var dialect = new ProviderDialect(source, new RegexRenderer());
        var table = new Table("Customer", [new Column("Name", DbType.String,
            constraints: [new RegexCheckConstraint(new ColumnIdentityExpression("Name"), "owner's", RegexNullBehavior.RejectNull)])]);

        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([table], []));

        Assert.That(sql, Does.Contain("CHECK ([Name] IS NOT NULL AND provider_match([Name], 'owner''s'))"));
    }

    [Test]
    public void ObjectNameOverride_IsUsedForTablesAndForeignKeyTargets()
    {
        var dialect = new ProviderDialect(source, new NameRenderer());
        var table = new Table(new DatabaseObjectName("Child", schema: "sales"), [new Column("ParentId", DbType.Int32)],
            [new ForeignKey("FK_Parent", ["ParentId"], new DatabaseObjectName("Parent", schema: "sales"), ["Id"])]);

        var parent = new Table(new DatabaseObjectName("Parent", schema: "sales"), [new Column("Id", DbType.Int32)]);
        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([parent, table], []));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("CREATE TABLE [provider_sales_Child]"));
            Assert.That(sql, Does.Contain("ALTER TABLE [provider_sales_Child]"));
            Assert.That(sql, Does.Contain("REFERENCES [provider_sales_Parent] ([Id])"));
        });
    }

    [Test]
    public void ForeignKeyPlacementOverride_DoesNotRequireSqliteIdentitySyntax()
    {
        var dialect = new ProviderDialect(source, new InlineForeignKeyRenderer());
        var table = new Table("Child", [new Column("ParentId", DbType.Int32)],
            [new ForeignKey("FK_Parent", ["ParentId"], "Parent", ["Id"])]);

        var parent = new Table("Parent", [new Column("Id", DbType.Int32)]);
        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([parent, table], []));

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Contain("CONSTRAINT [FK_Parent] FOREIGN KEY ([ParentId]) REFERENCES [Parent] ([Id])"));
            Assert.That(sql, Does.Not.Contain("ALTER TABLE"));
        });
    }

    [Test]
    public void ExistingInterfaceImplementation_UsesDefaultRendererAndCapabilities()
    {
        IDialect dialect = new CapabilityOnlyDialect(source);
        var table = new Table("Order", [new Column("Id", DbType.Int64, generation: new IdentityColumnGeneration(10, 2))]);

        var sql = new SchemaScriptRenderer(dialect).Render(new Schema([table], []));

        Assert.That(sql, Does.Contain("[Id] BIGINT IDENTITY(10, 2)"));
    }

    // These strategies use only the Core contract, just as a provider assembly would.
    private sealed class IdentityRenderer : StandardSchemaSqlRenderer
    {
        public override string RenderIdentity(IDialect dialect, string type, long seed, long increment)
            => $"{type} PROVIDER_IDENTITY({seed}, {increment})";
    }

    private sealed class GeneratedRenderer : StandardSchemaSqlRenderer
    {
        public override string RenderGenerated(IDialect dialect, string type, string expression, bool stored)
            => $"{type} PROVIDER_GENERATED({expression}, {stored})";
    }

    private sealed class RegexRenderer : StandardSchemaSqlRenderer
    {
        public override string RenderRegex(IDialect dialect, string expression, string pattern)
            => $"provider_match({expression}, {pattern})";
    }

    private sealed class NameRenderer : StandardSchemaSqlRenderer
    {
        public override string RenderObjectName(IDialect dialect, string? catalog, string? database, string? schema, string name)
            => dialect.Renderer.Render($"provider_{schema}_{name}", "identity");
    }

    private sealed class InlineForeignKeyRenderer : StandardSchemaSqlRenderer
    {
        public override bool RenderForeignKeysInline(IDialect dialect) => true;
    }

    private sealed class ProviderDialect(IDialect source, ISchemaSqlRenderer renderer)
        : BaseDialect(source.Language, source.Aliases, source.Renderer, source.Casters, source.DbTypeMapper, source.SqlFunctionMapper)
    {
        public override DialectCapabilities Capabilities => source.Capabilities;
        public override ISchemaSqlRenderer SchemaSqlRenderer => renderer;
    }

    private sealed class CapabilityOnlyDialect(IDialect source) : IDialect
    {
        public IRenderer Renderer => source.Renderer;
        public ICaster[] Casters => source.Casters;
        public string[] Aliases => source.Aliases;
        public ILanguage Language => source.Language;
        public IDbTypeMapper DbTypeMapper => source.DbTypeMapper;
        public ISqlFunctionMapper SqlFunctionMapper => source.SqlFunctionMapper;
        public DialectCapabilities Capabilities => source.Capabilities;
    }
}
