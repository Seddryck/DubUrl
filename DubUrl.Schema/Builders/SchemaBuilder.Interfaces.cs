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

public interface ISchemaBuilder
{
    Schema Build();
}
