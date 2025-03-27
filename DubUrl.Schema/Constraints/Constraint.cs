using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Morestachio.Framework.Expression;

namespace DubUrl.Schema.Constraints;

/// <summary>
/// Base class for database constraints that can be applied to tables or columns.
/// </summary>
public interface IConstraint
{ }

/// <summary>
/// Base class for database constraints that can be applied to tables or columns.
/// </summary>
public abstract class Constraint : IConstraint
{
    public string? Name { get; }
    protected Constraint(string? name)
        => Name = name;
}














