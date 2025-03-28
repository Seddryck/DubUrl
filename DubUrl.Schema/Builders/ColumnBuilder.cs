using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public class ColumnBuilder : IColumnName, IColumnTypeBuilder, IColumnNumericBuilder, IColumnTypeSettingsBuilder, IColumnConstraintBuilder
{
    public string Name { get; private set; } = string.Empty;
    private DbType Type { get; set; } = DbType.Object;
    private int? Length { get; set; }
    private int? Scale { get; set; }
    private object? DefaultValue { get; set; }
    private ColumnConstraintCollectionBuilder Constraints { get; } = [];

    public IColumnTypeBuilder WithName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty or whitespace.", nameof(name));
        Name = name;
        return this;
    }

    IColumnTypeSettingsBuilder IColumnTypeBuilder.WithType(DbType type)
    {
        Type = type;
        return this;
    }

    IColumnConstraintBuilder IColumnTypeBuilder.WithLength(int value)
    {
        Length = value;
        return this;
    }

    IColumnNumericBuilder IColumnTypeBuilder.WithPrecision(int value)
    {
        Length = value;
        return this;
    }

    IColumnConstraintBuilder IColumnNumericBuilder.WithScale(int value)
    {
        if (value>=Length)
            throw new ArgumentOutOfRangeException(nameof(value), "Scale must be less than precision");
        Scale = value;
        return this;
    }

    IColumnConstraintBuilder IColumnConstraintBuilder.WithDefaultValue(object value)
    {
        DefaultValue = value;
        return this;
    }
    IColumnConstraintBuilder IColumnConstraintBuilder.WithNullable(bool value)

    {
        if (value)
            Constraints.AddNullable();
        else
            Constraints.AddNotNullable();
        return this;
    }

    IColumnConstraintBuilder IColumnConstraintBuilder.WithNullable()

    {
        Constraints.AddNullable();
        return this;
    }
    IColumnConstraintBuilder IColumnConstraintBuilder.WithNotNullable()
    {
        Constraints.AddNotNullable();
        return this;
    }
    IColumnConstraintBuilder IColumnConstraintBuilder.WithUnique()
    {
        Constraints.AddUnique();
        return this;
    }
    IColumnConstraintBuilder IColumnConstraintBuilder.WithPrimaryKey()
    {
        Constraints.AddPrimaryKey();
        return this;
    }
    IColumnConstraintBuilder IColumnConstraintBuilder.WithCheck(Func<ICheckBuilder, ICheckBuildable> builder)
    {
        Constraints.AddCheck(builder(new CheckBuilder(this)).Build());
        return this;
    }

    Column IColumnBuilder.Build()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Column name must be set before building");

        var constraints = Constraints.Build();

        if (Scale.HasValue && Length.HasValue)
            return new NumericColumn(Name, Type, Length.Value, Scale.Value, DefaultValue, constraints);

        if (Length.HasValue)
            return new VarLengthColumn(Name, Type, Length.Value, DefaultValue, constraints);

        return new Column(Name, Type, DefaultValue, constraints);
    }
}
