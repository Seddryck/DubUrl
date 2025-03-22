using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface ITableCollectionBuilder
{
    ISchemaBuilder WithTables(Func<TableCollectionBuilder, TableCollectionBuilder> tables);
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
