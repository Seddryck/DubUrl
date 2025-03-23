using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class SchemaBuilder : ISchemaBuilder, ITableCollectionBuilder
{
    private TableCollectionBuilder Tables { get; set; } = new();

    public ISchemaBuilder WithTables(Func<TableCollectionBuilder, TableCollectionBuilder> tables)
    {
        Tables = tables(Tables);
        return this;
    }

    public Schema Build()
    {
        var tables = Tables.Select(c => c.Build()).ToArray();

        if (tables.GroupBy(c => c.Name).Count() != tables.Length)
            throw new InvalidOperationException("Table names must be unique.");

        return new Schema(tables);
    }
}
