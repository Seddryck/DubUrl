using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface ITableCollectionBuilder
{
    IIndexCollectionBuilder WithTables(Func<TableCollectionBuilder, TableCollectionBuilder> tables);
}

public interface IIndexCollectionBuilder : ISchemaBuilder
{
    ISchemaBuilder WithIndexes(Func<IndexCollectionBuilder, IndexCollectionBuilder> indexes);
}

/// <summary>
/// Interface for schema building operations.
/// </summary>
public interface ISchemaBuilder
{
    /// <summary>
    /// Builds and returns a Schema object.
    /// </summary>
    /// <returns>A fully constructed Schema object.</returns>
    Schema Build();
}
