using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Constraints;
using NUnit.Framework;

namespace DubUrl.Schema.Testing.Builders;

public class SchemaBuilderTests
{
    [Test]
    public void Build_Simple_Expected()
    {
        var builder = new SchemaBuilder()
                            .WithTables(tables =>
                                tables.Add(table => table.WithName("Customer")
                                                        .WithColumns(cols => cols.Add(col => col.WithName("CustomerId").WithType(DbType.Int32))))
                                    .Add(table => table.WithName("Sales")
                                                        .WithColumns(cols => cols.Add(col => col.WithName("SalesId").WithType(DbType.Int64))))
                            );
        var schema = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema, Is.TypeOf<Schema>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(schema.Tables, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_SimpleWithIndex_Expected()
    {
        var builder = new SchemaBuilder()
                            .WithTables(tables =>
                                tables.Add(table => table.WithName("Customer")
                                                        .WithColumns(cols => cols.Add(col => col.WithName("CustomerId").WithType(DbType.Int32))))
                                    .Add(table => table.WithName("Sales")
                                                        .WithColumns(cols => cols.Add(col => col.WithName("SalesId").WithType(DbType.Int64)))))
                            .WithIndexes(indexes =>
                                indexes.Add(index => index.WithName("IX_CustomerId")
                                                          .OnTable("Customer")
                                                          .WithColumns(cols => cols.Add(col => col.WithName("CustomerId"))))
                                       .Add(index => index.WithName("IX_SalesId")
                                                          .OnTable("Sales")
                                                          .WithColumns(cols => cols.Add(col => col.WithName("SalesId"))))
                            );
        var schema = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(schema, Is.Not.Null);
            Assert.That(schema, Is.TypeOf<Schema>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(schema.Tables, Has.Count.EqualTo(2));
            Assert.That(schema.Indexes, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public void Build_WithQualifiedViewChain_PreservesDefinitions()
    {
        var source = new DatabaseObjectName("customers", schema: "sales");
        var first = new DatabaseObjectName("customer_names", schema: "reporting");
        var schema = new SchemaBuilder()
            .WithTables(tables => tables.Add(table => table
                .WithIdentity(source)
                .WithColumns(columns => columns
                    .Add(column => column.WithName("id").WithType(DbType.Int32))
                    .Add(column => column.WithName("name").WithType(DbType.String)))))
            .WithViews(views => views
                .Add(view => view.WithIdentity(first)
                    .From(source)
                    .WithProjections(projections => projections
                        .Add(projection => projection.WithColumn("id"))
                        .Add(projection => projection.WithFunction(
                            "Lower", new ColumnIdentityExpression("name")).As("normalized_name"))))
                .Add(view => view.WithName("customer_names_copy")
                    .From(first)
                    .WithProjections(projections => projections
                        .Add(projection => projection.WithColumn("normalized_name")))))
            .Build();

        Assert.Multiple(() =>
        {
            Assert.That(schema.Views, Has.Count.EqualTo(2));
            Assert.That(schema.Views.Values.First().Identity, Is.EqualTo(first));
            Assert.That(schema.Views.Values.Last().Query.Source, Is.EqualTo(first));
            Assert.That(schema.Views.Values.First().Query.Projections.Last().Alias, Is.EqualTo("normalized_name"));
        });
    }
}
