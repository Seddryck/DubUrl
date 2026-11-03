namespace DubUrl.Querying.Dialects;

/// <summary>Semantic operation supplied before SQL is concatenated. Never inferred by parsing SQL.</summary>
public enum SchemaSqlOperation
{
    DropView,
    DropIndex,
    DropTable,
    IdentityPrerequisite,
    CreateTable,
    ForeignKey,
    Comment,
    CreateIndex,
    CreateView,
}

/// <summary>A complete executable statement or batch, with optional diagnostic identity.</summary>
/// <param name="CommandText">SQL whose internal boundaries must remain intact.</param>
/// <param name="Operation">The operation represented by this command.</param>
/// <param name="ObjectName">An object label for diagnostics, without connection information.</param>
/// <param name="IsProceduralBlock">Whether a provider must preserve the block's final terminator.</param>
public sealed record SchemaSqlCommand(string CommandText, SchemaSqlOperation Operation,
    string? ObjectName = null, bool IsProceduralBlock = false);
