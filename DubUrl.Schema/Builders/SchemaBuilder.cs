using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class SchemaBuilder : ISchemaBuilder, ITableCollectionBuilder, IIndexCollectionBuilder, IViewCollectionBuilder
{
    private TableCollectionBuilder Tables { get; set; } = new();
    private IndexCollectionBuilder Indexes { get; set; } = new();
    private ViewCollectionBuilder Views { get; set; } = new();

    public IIndexCollectionBuilder WithTables(Func<TableCollectionBuilder, TableCollectionBuilder> tables)
    {
        Tables = tables(Tables);
        return this;
    }

    IViewCollectionBuilder IIndexCollectionBuilder.WithIndexes(Func<IndexCollectionBuilder, IndexCollectionBuilder> indexes)
    {
        Indexes = indexes(Indexes);
        return this;
    }

    ISchemaBuilder IViewCollectionBuilder.WithViews(Func<ViewCollectionBuilder, ViewCollectionBuilder> views)
    {
        Views = views(Views);
        return this;
    }

    Schema ISchemaBuilder.Build()
    {
        var tables = Tables.Select(c => c.Build()).ToArray();

        if (tables.GroupBy(c => c.Identity).Count() != tables.Length)
            throw new InvalidOperationException("Table names must be unique.");

        var indexes = Indexes.Select(c => c.Build()).ToArray();

        if (indexes.GroupBy(c => c.Identity).Count() != indexes.Length)
            throw new InvalidOperationException("Index names must be unique.");

        var views = Views.Select(view => view.Build()).ToArray();

        if (views.GroupBy(view => view.Identity).Count() != views.Length)
            throw new InvalidOperationException("View names must be unique.");

        return new Schema(tables, indexes, views);
    }
}
