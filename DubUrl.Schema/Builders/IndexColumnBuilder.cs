using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DubUrl.Schema.Constraints;

namespace DubUrl.Schema.Builders;

public class IndexColumnBuilder : IIndexColumnBuilder
{
    public string Name { get; private set; } = string.Empty;

    public IIndexColumnBuilder WithName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be empty or whitespace.", nameof(name));
        Name = name;
        return this;
    }

    IndexColumn IIndexColumnBuilder.Build()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Column name must be set before building");

        return new IndexColumn(Name);
    }
}
