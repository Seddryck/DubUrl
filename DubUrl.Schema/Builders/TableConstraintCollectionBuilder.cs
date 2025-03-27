using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;
public class TableConstraintCollectionBuilder : IEnumerable<IConstraintBuilder>
{
    private List<IConstraintBuilder> Constraints { get; } = [];

    public TableConstraintCollectionBuilder AddPrimaryKey(Func<PrimaryKeyConstraintBuilder, IConstraintBuilder> column)
    {
        Constraints.Add(column(new()));
        return this;
    }

    public IEnumerator<IConstraintBuilder> GetEnumerator()
        => Constraints.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
