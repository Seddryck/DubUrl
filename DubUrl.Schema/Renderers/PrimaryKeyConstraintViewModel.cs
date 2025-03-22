using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Renderers;

/// <summary>
/// Renders a primary key constraint for SQL schema generation.
/// </summary>
public class PrimaryKeyConstraintViewModel
{
    public string? Name { get; }
    public Column[] Columns { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PrimaryKeyConstraintViewModel"/> class.
    /// </summary>
    /// <param name="pk">The primary key constraint to render.</param>
    /// <exception cref="ArgumentNullException">Thrown when pk is null.</exception>
    public PrimaryKeyConstraintViewModel(PrimaryKeyConstraint pk)
    {
        ArgumentNullException.ThrowIfNull(pk, nameof(pk));
        Name = pk.Name;
        Columns = pk.Columns.Values.ToArray();
    }
}
