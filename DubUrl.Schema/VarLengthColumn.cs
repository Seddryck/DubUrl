using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema;
internal class VarLengthColumn : Column
{
    public int Size { get; }

    public VarLengthColumn(string name, DbType type, int size, bool isNullable = false, object? defaultValue = null)
        : base(name, type, isNullable, defaultValue)
        => Size = size;
}
