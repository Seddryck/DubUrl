using DubUrl.Schema;

namespace DubUrl.Application;

/// <summary>Options controlling schema script rendering.</summary>
public sealed record SchemaRenderOptions
{
    public SchemaCreationOptions CreationOptions { get; init; } = SchemaCreationOptions.None;
}

/// <summary>Options controlling schema deployment.</summary>
public sealed record SchemaDeploymentOptions
{
    public SchemaCreationOptions CreationOptions { get; init; } = SchemaCreationOptions.None;
}
