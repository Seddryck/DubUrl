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
public interface ICheckBuilder
{
    ICheckBuildable WithComparison(Func<ICheckExpressionBuilder, ICheckExpressionBuildable> left, string op, Func<ICheckExpressionValueBuilder, ICheckExpressionBuildable> right);
}

public interface ICheckBuildable
{    /// <summary>
     /// Builds and returns a Check constraint object based on the configured properties.
     /// </summary>
     /// <returns>A Check constraint object with the configured properties.</returns>
    CheckConstraint Build();
}
