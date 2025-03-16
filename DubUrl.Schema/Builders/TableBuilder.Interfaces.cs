using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface ITableColumnCollectionBuilder
{
    ITableBuilder WithColumns(Func<ColumnCollectionBuilder, ColumnCollectionBuilder> columns);
}

public interface ITableBuilder
{
    Table Build();
}
