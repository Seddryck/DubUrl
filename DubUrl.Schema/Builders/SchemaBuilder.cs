using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class SchemaBuilder : ISchemaBuilder, ITableCollectionBuilder, IIndexCollectionBuilder
{
    private TableCollectionBuilder Tables { get; set; } = new();
    private IndexCollectionBuilder Indexes { get; set; } = new();

    public IIndexCollectionBuilder WithTables(Func<TableCollectionBuilder, TableCollectionBuilder> tables)
    {
        Tables = tables(Tables);
        return this;
    }

    ISchemaBuilder IIndexCollectionBuilder.WithIndexes(Func<IndexCollectionBuilder, IndexCollectionBuilder> indexes)
    {
        Indexes = indexes(Indexes);
        return this;
    }

    Schema ISchemaBuilder.Build()
    {
        var tables = Tables.Select(c => c.Build()).ToArray();

        if (tables.GroupBy(c => c.Name).Count() != tables.Length)
            throw new InvalidOperationException("Table names must be unique.");

        var indexes = Indexes.Select(c => c.Build()).ToArray();

        if (indexes.GroupBy(c => c.Name).Count() != indexes.Length)
            throw new InvalidOperationException("Index names must be unique.");

        return new Schema(tables, indexes);
    }
}
