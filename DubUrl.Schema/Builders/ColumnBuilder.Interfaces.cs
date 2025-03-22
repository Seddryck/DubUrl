using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DubUrl.Schema.Builders;

public interface IColumnTypeBuilder
{
    IColumnTypeSettingsBuilder WithType(DbType type);
    IColumnSettingsBuilder WithLength(int value);
    IColumnNumericBuilder WithPrecision(int value);
}

public interface IColumnNumericBuilder
{
    IColumnSettingsBuilder WithScale(int value);
}

public interface IColumnTypeSettingsBuilder : IColumnTypeBuilder, IColumnSettingsBuilder
{ }

public interface IColumnSettingsBuilder : IColumnBuilder
{
    IColumnSettingsBuilder WithDefaultValue(object value);
    IColumnSettingsBuilder AsNullable(bool value);
    IColumnSettingsBuilder AsNullable();
    IColumnSettingsBuilder AsNotNullable();
}

public interface IColumnBuilder
{
    Column Build();
}
