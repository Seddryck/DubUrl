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

/// <summary>
/// Base interface for all index column builders.
/// </summary>
public interface IIndexColumnBuilder
{
    /// <summary>
    /// Builds and returns an Index Column object based on the configured properties.
    /// </summary>
    /// <returns>An Index Column object with the configured properties.</returns>
    IndexColumn Build();
}

