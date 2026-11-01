namespace DubUrl.Querying.Dialects;

/// <summary>Clauses supplied by a provider's column specification instead of shared templates.</summary>
[Flags]
public enum ColumnDefinitionClauses
{
    None = 0,
    Generation = 1,
    PrimaryKey = 2,
    Nullability = 4,
    Unique = 8,
    Default = 16,
    Checks = 32,
    Comment = 64,
}

/// <summary>Provider-neutral generation inputs; expressions are already rendered SQL.</summary>
public sealed record ColumnGenerationContext(bool Identity, long Seed = 1, long Increment = 1,
    string? Expression = null, bool Stored = true);

/// <summary>Resolved column and containing primary-key context, without Schema model dependencies.</summary>
/// <remarks>PrimaryKeyColumns describes the whole key, including normalized table-level keys.
/// Nullable and NotNullable reflect explicit declarations. Primary keys imply non-null values.</remarks>
public sealed record ColumnDefinitionContext(SchemaSqlObjectName? Table, string Column, string Type,
    IReadOnlyList<string> PrimaryKeyColumns, string? PrimaryKeyName, bool TablePrimaryKey,
    bool Nullable, bool NotNullable, bool Unique, ColumnGenerationContext? Generation,
    string? DefaultExpression = null, IReadOnlyList<string>? Checks = null, string? Description = null);

/// <summary>A complete type/generation specification, excluding the column name.</summary>
/// <remarks>When generation is present, Generation must be claimed. Claiming PrimaryKey consumes
/// the whole key and is only valid for a single-column key on this column.</remarks>
public sealed record ColumnDefinitionPlan(string Specification, ColumnDefinitionClauses Clauses);

/// <summary>Optional table-aware column rendering. Return null to use existing generation hooks.</summary>
/// <remarks>Consumers must support this contract before a provider advertises dependent capabilities.</remarks>
public interface IColumnDefinitionSqlRenderer
{
    ColumnDefinitionPlan? RenderColumnDefinition(IDialect dialect, ColumnDefinitionContext column);
}
