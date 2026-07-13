using NSchema.Project.Domain.Models;

namespace NSchema.Operations;

/// <summary>
/// Arguments for an operation run.
/// </summary>
public sealed record DriftArguments
{
    /// <summary>
    /// The schemas to scope the drift check to. When <see langword="null"/>, the whole recorded state is checked.
    /// </summary>
    public SchemaScope Scope { get; init; } = SchemaScope.All;
}
