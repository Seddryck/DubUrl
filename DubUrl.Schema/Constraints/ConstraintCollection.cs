using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Constraints;
public class ConstraintCollection : List<IConstraint>
{
    public ConstraintCollection()
        : this([])
    { }
    public ConstraintCollection(IConstraint[] constraints)
        => AddRange(constraints);

    public T? Get<T>() where T : Constraint
        => this.OfType<T>().FirstOrDefault();
}
