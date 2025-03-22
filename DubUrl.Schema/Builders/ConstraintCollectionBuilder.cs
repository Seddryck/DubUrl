using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;
public class ConstraintCollectionBuilder : IEnumerable<IConstraintBuilder>
{
    private List<IConstraintBuilder> Constraints { get; } = [];

    public ConstraintCollectionBuilder Add(Func<ColumnBuilder, IConstraintBuilder> column)
    {
        var constraint = column(new()) ?? throw new ArgumentException("The constraint builder function returned null", nameof(column));
        Constraints.Add(constraint);
        return this;
    }

    public ConstraintCollectionBuilder AddPrimaryKey(Func<PrimaryKeyConstraintBuilder, IConstraintBuilder> column)
    {
        Constraints.Add(column(new()));
        return this;
    }

    public IEnumerator<IConstraintBuilder> GetEnumerator()
        => Constraints.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
