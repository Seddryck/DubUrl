using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema;
public class VarLengthColumn : Column
{
    public int Length { get; }

    public VarLengthColumn(string name, System.Data.DbType type, int size, object? defaultValue = null, IConstraint[]? constraints = null)
        : base(name, type, defaultValue, constraints)
        => Length = size;
}
