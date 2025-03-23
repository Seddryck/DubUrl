using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface ITableColumnCollectionBuilder
{
    ITableConstraintCollectionBuilder WithColumns(Func<ColumnCollectionBuilder, ColumnCollectionBuilder> columns);
}

public interface ITableConstraintCollectionBuilder : ITableBuilder
{
    ITableBuilder WithConstraints(Func<ConstraintCollectionBuilder, ConstraintCollectionBuilder> constraints);
}

public interface ITableBuilder
{
    Table Build();
}
