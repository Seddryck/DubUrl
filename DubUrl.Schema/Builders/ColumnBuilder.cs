using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public class ColumnBuilder : IColumnTypeBuilder, IColumnNumericBuilder, IColumnTypeSettingsBuilder, IColumnSettingsBuilder
{
    private string Name { get; set; } = string.Empty;
    private DbType Type { get; set; } = DbType.Object;
    private int? Length { get; set; }
    private int? Scale { get; set; }
    private object? DefaultValue { get; set; }
    private bool IsNullable { get; set; } = false;

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

    IColumnSettingsBuilder IColumnTypeBuilder.WithLength(int value)
    {
        Length = value;
        return this;
    }

    IColumnNumericBuilder IColumnTypeBuilder.WithPrecision(int value)
    {
        Length = value;
        return this;
    }

    IColumnSettingsBuilder IColumnNumericBuilder.WithScale(int value)
    {
        if (value>=Length)
            throw new ArgumentOutOfRangeException(nameof(value), "Scale must be less than precision");
        Scale = value;
        return this;
    }

    IColumnSettingsBuilder IColumnSettingsBuilder.WithDefaultValue(object value)
    {
        DefaultValue = value;
        return this;
    }

    IColumnSettingsBuilder IColumnSettingsBuilder.AsNullable(bool value)
    {
        IsNullable = value;
        return this;
    }

    IColumnSettingsBuilder IColumnSettingsBuilder.AsNullable()
        => ((IColumnSettingsBuilder)this).AsNullable(true);

    IColumnSettingsBuilder IColumnSettingsBuilder.AsNotNullable()
        => ((IColumnSettingsBuilder)this).AsNullable(false);

    Column IColumnBuilder.Build()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Column name must be set before building");

        if (Scale.HasValue && Length.HasValue)
            return new NumericColumn(Name, Type, Length.Value, Scale.Value, IsNullable, DefaultValue);

        if (Length.HasValue)
            return new VarLengthColumn(Name, Type, Length.Value, IsNullable, DefaultValue);

        return new Column(Name, Type, IsNullable, DefaultValue);
    }
}
