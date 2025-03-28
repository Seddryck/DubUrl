using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;
public class ColumnConstraintCollectionBuilder : IEnumerable<IConstraint>
{
    private List<IConstraint> Constraints { get; } = [];

    public ColumnConstraintCollectionBuilder AddPrimaryKey()
    {
        Constraints.Add(new PrimaryKeyConstraint());
        return this;
    }

    public ColumnConstraintCollectionBuilder AddUnique()
    {
        Constraints.Add(new UniquenessConstraint());
        return this;
    }

    public ColumnConstraintCollectionBuilder AddNullable()
    {
        Constraints.Add(new NullableConstraint());
        return this;
    }

    public ColumnConstraintCollectionBuilder AddNotNullable()
    {
        Constraints.Add(new NotNullableConstraint());
        return this;
    }

    public ColumnConstraintCollectionBuilder AddCheck(CheckConstraint check)
    {
        Constraints.Add(check);
        return this;
    }

    public IConstraint[] Build()
        => Constraints.ToArray();

    public IEnumerator<IConstraint> GetEnumerator()
        => Constraints.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}
