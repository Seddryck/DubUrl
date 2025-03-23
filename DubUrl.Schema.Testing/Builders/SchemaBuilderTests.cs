using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
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
}
