namespace DubUrl.Querying.Dialects;

/// <summary>Optional inline CREATE TABLE description rendering, independent of physical table options.</summary>
/// <remarks>Consumers append the returned clause after the closing parenthesis and before the statement
/// terminator. Do not also call RenderComment for the table. Column comments keep their existing path.</remarks>
public interface ITableDescriptionSqlRenderer
{
    /// <summary>Returns a table option including its leading space; identifiers are literal components.</summary>
    string RenderTableDescription(IDialect dialect, SchemaSqlObjectName table, string description);
}
