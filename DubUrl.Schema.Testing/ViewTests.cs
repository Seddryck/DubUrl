using DubUrl.Schema.Constraints;
using NUnit.Framework;

namespace DubUrl.Schema.Testing;

public class ViewTests
{
    [Test]
    public void Create_QualifiedViewChain_PreservesOrderAndStructuredQueries()
    {
        var table = new Table(new DatabaseObjectName("customers", schema: "sales"),
            [new Column("id", System.Data.DbType.Int32)]);
        var first = new View(
            new DatabaseObjectName("customer_ids", schema: "reporting"),
            new ViewQuery(table.Identity, new ViewProjection(new ColumnIdentityExpression("id"))));
        var second = new View(
            new DatabaseObjectName("customer_ids_copy", schema: "reporting"),
            new ViewQuery(first.Identity, new ViewProjection(new ColumnIdentityExpression("id"), "customer_id")));

        var schema = new Schema([table], [], [first, second]);

        Assert.Multiple(() =>
        {
            Assert.That(schema.Views.Keys, Is.EqualTo(new[] { first.Identity.Key, second.Identity.Key }));
            Assert.That(schema.GetView(second.Identity).Query.Source, Is.EqualTo(first.Identity));
            Assert.That(schema.GetView(second.Identity).Query.Projections.Single().Alias, Is.EqualTo("customer_id"));
        });
    }

    [Test]
    public void Create_ViewWithMissingSource_ThrowsActionableDiagnostic()
    {
        var view = new View("customer_view", new ViewQuery(
            new DatabaseObjectName("missing"),
            new ViewProjection(new ColumnIdentityExpression("id"))));

        Assert.That(
            () => new Schema([], [], [view]),
            Throws.ArgumentException.With.Message.Contains("references missing source relation 'missing'"));
    }

    [Test]
    public void Create_TableViewIdentityCollision_ThrowsActionableDiagnostic()
    {
        var table = new Table("customer", [new Column("id", System.Data.DbType.Int32)]);
        var view = new View("customer", new ViewQuery(
            table.Identity,
            new ViewProjection(new ColumnIdentityExpression("id"))));

        Assert.That(
            () => new Schema([table], [], [view]),
            Throws.ArgumentException.With.Message.Contains("Table and view identities must be unique: customer"));
    }

    [Test]
    public void Create_DuplicateProjectionAliases_ThrowsActionableDiagnostic()
        => Assert.That(
            () => new ViewQuery(
                new DatabaseObjectName("customer"),
                new ViewProjection(new ColumnIdentityExpression("id"), "value"),
                new ViewProjection(new ColumnIdentityExpression("name"), "value")),
            Throws.ArgumentException.With.Message.Contains("View projection aliases must be unique: value"));
}
