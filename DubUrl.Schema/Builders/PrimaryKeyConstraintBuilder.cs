using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace DubUrl.Schema.Builders;

public class PrimaryKeyConstraintBuilder : IConstraintBuilder
{
    private string[]? Names { get; set; }

    public IConstraintBuilder WithColumnNames(params string[] names)
    {
        Names = names;
        return this;
    }

    public IConstraintBuilder WithColumnName(string name)
        => WithColumnNames([name]);

    Constraint IConstraintBuilder.Build()
    {
        if (Names is null || Names.Length==0)
            throw new InvalidDataException("Column names must be provided.");

        var columns = Names.Select(name => new Column(name, DbType.Object));

        return new PrimaryKeyConstraint(columns.ToArray());
    }
}
