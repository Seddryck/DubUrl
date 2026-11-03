using DubUrl.Provider.DuckDb.Sql;
using DubUrl.Provider.Oracle.Sql;
using DubUrl.Provider.SqlServer.Sql;
using DubUrl.Querying.Dialects;
using NUnit.Framework;

namespace DubUrl.Testing;

public class SchemaCommandPlanningTests
{
    private static IDialect Dialect<T>()
        where T : IDialect
    {
        var builder = new DialectRegistryBuilder();
        builder.AddDialect<T>(["test"]);
        return builder.Build().Get<T>();
    }

    [Test]
    public void StandardPlanner_PreservesWholeScopesAndDelegatesToFallback()
    {
        var dialect = Dialect<OracleDialect>();
        var block = new SchemaSqlCommand("BEGIN NULL; NULL; END;", SchemaSqlOperation.IdentityPrerequisite, IsProceduralBlock: true);
        var sql = new SchemaSqlCommand("COMMENT ON TABLE t IS 'a;b';", SchemaSqlOperation.Comment);
        var defaultPlan = StandardSchemaSqlRenderer.Instance.PlanCommands(dialect, [block, sql]);
        Assert.That(defaultPlan, Is.EqualTo(new[] { block, sql }));
        var delegated = new StandardSchemaSqlRenderer(dialect.SchemaSqlRenderer).PlanCommands(dialect, [block, sql]);
        Assert.That(delegated[0], Is.EqualTo(block));
        Assert.That(delegated[1].CommandText, Is.EqualTo("COMMENT ON TABLE t IS 'a;b'"));
    }

    [Test]
    public void SqlServerPlanner_GroupsConsecutiveCommentsButNeverAcrossViewBoundaries()
    {
        var dialect = Dialect<TSqlDialect>();
        var comments = new[] {
            new SchemaSqlCommand("EXEC p 'a;b';", SchemaSqlOperation.Comment, "a"),
            new SchemaSqlCommand("EXEC p 'c;d';", SchemaSqlOperation.Comment, "b") };
        var view = new SchemaSqlCommand("CREATE VIEW v AS SELECT 1 AS id;", SchemaSqlOperation.CreateView, "v");
        var plan = dialect.SchemaSqlRenderer.PlanCommands(dialect, [.. comments, view, comments[0]]);
        Assert.That(plan, Has.Count.EqualTo(3));
        Assert.That(plan[0].CommandText, Is.EqualTo(string.Join("\r\n", comments.Select(c => c.CommandText))));
        Assert.That(plan[1], Is.EqualTo(view));
        Assert.That(plan[2].CommandText, Is.EqualTo(comments[0].CommandText));
    }

    [Test]
    public void DuckDbPrerequisites_KeepLegacyExportButExposeSeparateDropAndCreate()
    {
        var dialect = Dialect<DuckDbDialect>();
        var renderer = (ISequenceIdentitySqlRenderer)dialect.SchemaSqlRenderer;
        var context = new IdentityColumnContext("table;name", "id;name");
        var plan = renderer.PlanIdentityPrerequisites(dialect, context, 10, 2, true);
        Assert.That(plan, Has.Count.EqualTo(2));
        Assert.That(plan[0].CommandText, Does.StartWith("DROP SEQUENCE"));
        Assert.That(plan[1].CommandText, Does.StartWith("CREATE SEQUENCE"));
        Assert.That(renderer.RenderIdentityPrerequisite(dialect, context, 10, 2, true),
            Is.EqualTo(string.Join("\r\n", plan.Select(c => c.CommandText))));
        Assert.That(renderer.PlanIdentityPrerequisites(dialect, context, 10, 2, false), Has.Count.EqualTo(1));
    }

    [Test]
    public void LegacySequenceRenderer_RejectsDeploymentInsteadOfParsingItsScript()
    {
        ISequenceIdentitySqlRenderer renderer = new LegacySequenceRenderer();
        Assert.Throws<NotSupportedException>(() => renderer.PlanIdentityPrerequisites(Dialect<DuckDbDialect>(), new("t", "id"), 1, 1, true));
    }

    private sealed class LegacySequenceRenderer : ISequenceIdentitySqlRenderer
    {
        public string RenderIdentityPrerequisite(IDialect dialect, IdentityColumnContext column, long seed, long increment, bool dropExisting)
            => "DROP SEQUENCE s; CREATE SEQUENCE s;";
        public string RenderIdentityColumn(IDialect dialect, IdentityColumnContext column, string type) => type;
    }
}
