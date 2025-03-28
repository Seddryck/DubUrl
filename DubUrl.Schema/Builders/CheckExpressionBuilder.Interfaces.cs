using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

/// <summary>
/// Base interface for all check constraint builders.
/// </summary>
public interface ICheckExpressionBuilder
{
    ICheckExpressionBuildable WithCurrentColumn();
    //ICheckExpressionBuildable WithColumn(Column column);
    //ICheckExpressionBuildable WithFunctionColumn(string function, Column column);
    ICheckExpressionBuildable WithFunctionCurrentColumn(string function);
}

public interface ICheckExpressionValueBuilder
{
    ICheckExpressionBuildable WithValue(object value);
}

public interface ICheckExpressionBuildable
{    /// <summary>
     /// Builds and returns a Check constraint object based on the configured properties.
     /// </summary>
     /// <returns>A Check constraint object with the configured properties.</returns>
    Expression Build();
}
