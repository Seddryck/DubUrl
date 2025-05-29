using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Constraints;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace DubUrl.Schema.Testing.Builders;

public class IndexBuilderTests
{
    [Test]
    public void Build_Simple_Expected()
    {
        var builder = new IndexBuilder()
                            .WithName("idx")
                            .OnTable("table")
                            .WithColumns(cols =>
                                cols.Add(col => col.WithName("id"))
                                .Add(col => col.WithName("name"))
                            );
        var index = builder.Build();
        Assert.Multiple(() =>
        {
            Assert.That(index, Is.Not.Null);
            Assert.That(index, Is.TypeOf<Index>());
        });
        Assert.Multiple(() =>
        {
            Assert.That(index.Name, Is.EqualTo("idx"));
            Assert.That(index.TableName, Is.EqualTo("table"));
            Assert.That(index.Columns, Has.Count.EqualTo(2));
            Assert.That(index.Columns, Does.ContainKey("id"));
            Assert.That(index.Columns, Does.ContainKey("name"));
        });
    }
}
