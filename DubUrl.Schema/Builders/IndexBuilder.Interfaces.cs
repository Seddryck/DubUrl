using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface IIndexTableBuilder
{
    IIndexTableBuilder WithUnique(bool unique = true);
    IIndexColumnCollectionBuilder OnTable(string name);
    IIndexColumnCollectionBuilder OnTable(DatabaseObjectName identity);
}

public interface IIndexColumnCollectionBuilder
{
    IIndexBuilder WithColumns(Func<IndexColumnCollectionBuilder, IndexColumnCollectionBuilder> columns);
}

public interface IIndexBuilder
{
    Index Build();
}
