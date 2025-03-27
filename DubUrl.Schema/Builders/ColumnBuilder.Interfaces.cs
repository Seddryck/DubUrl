using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema;
using DubUrl.Schema.Builders;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public interface IColumnName
{
    string Name { get; }
}

/// <summary>
/// Interface for defining column type properties.
/// </summary>
public interface IColumnTypeBuilder
{
    /// <summary>
    /// Sets the database type for the column.
    /// </summary>
    /// <param name="type">The database type to set.</param>
    /// <returns>A builder interface for further configuration.</returns>
    IColumnTypeSettingsBuilder WithType(DbType type);
    /// <summary>
    /// Sets the length for the column.
    /// </summary>
    /// <param name="value">The length value.</param>
    /// <returns>A builder interface for further configuration.</returns>
    IColumnConstraintBuilder WithLength(int value);
    /// <summary>
    /// Sets the precision for numeric columns.
    /// </summary>
    /// <param name="value">The precision value.</param>
    /// <returns>A builder interface for further configuration of numeric properties.</returns>
    IColumnNumericBuilder WithPrecision(int value);
}

/// <summary>
/// Interface for configuring numeric column properties.
/// </summary>
public interface IColumnNumericBuilder
{
    /// <summary>
    /// Sets the scale (number of decimal places) for a numeric column.
    /// </summary>
    /// <param name="value">The scale value.</param>
    /// <returns>A builder interface for further configuration.</returns>
    IColumnConstraintBuilder WithScale(int value);
}

/// <summary>
/// Composite interface that combines column type definition and column settings capabilities.
/// </summary>
public interface IColumnTypeSettingsBuilder : IColumnTypeBuilder, IColumnConstraintBuilder
{ }

/// <summary>
/// Interface for configuring column settings such as default values and nullability.
/// </summary>
public interface IColumnConstraintBuilder : IColumnBuilder
{
    /// <summary>
    /// Sets the default value for the column.
    /// </summary>
    /// <param name="value">The default value.</param>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithDefaultValue(object value);
    /// <summary>
    /// Sets whether the column is nullable.
    /// </summary>
    /// <param name="value">True if the column is nullable; otherwise, false.</param>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithNullable(bool value);
    /// <summary>
    /// Sets the column as nullable.
    /// </summary>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithNullable();
    /// <summary>
    /// Sets the column as not nullable.
    /// </summary>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithNotNullable();
    /// <summary>
    /// Sets the column as having unique values.
    /// </summary>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithUnique();
    /// <summary>
    /// Sets the column as being the primary key
    /// </summary>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithPrimaryKey();
    /// <summary>
    /// Sets the column as being the primary key
    /// </summary>
    /// <returns>The settings builder for method chaining.</returns>
    IColumnConstraintBuilder WithCheck(Func<ICheckBuilder, ICheckBuildable> builder);
}

/// <summary>
/// Base interface for all column builders.
/// </summary>
public interface IColumnBuilder
{
    /// <summary>
    /// Builds and returns a Column object based on the configured properties.
    /// </summary>
    /// <returns>A Column object with the configured properties.</returns>
    Column Build();
}

