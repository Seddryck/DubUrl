namespace DubUrl.Querying.Dialects;

/// <summary>Literal table/column identity used when generation requires an auxiliary sequence.</summary>
public sealed record IdentityColumnContext(string Table, string Column, string? Schema = null, string? Database = null, string? Catalog = null);

/// <summary>Optional provider-owned rendering for sequence-backed portable identity columns.</summary>
public interface ISequenceIdentitySqlRenderer
{
    string RenderIdentityPrerequisite(IDialect dialect, IdentityColumnContext column, long seed, long increment, bool dropExisting);
    string RenderIdentityColumn(IDialect dialect, IdentityColumnContext column, string type);
}
