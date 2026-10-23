using System.Data;

namespace DubUrl.Querying.Templating;

/// <summary>A value with explicit ADO.NET metadata, including typed SQL nulls.</summary>
public sealed record TemplateParameter(object? Value, DbType DbType, int? Size = null, byte? Precision = null, byte? Scale = null);
