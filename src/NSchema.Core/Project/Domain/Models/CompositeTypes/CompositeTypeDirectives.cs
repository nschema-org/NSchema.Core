namespace NSchema.Project.Domain.Models.CompositeTypes;

/// <summary>
/// The management directives declared for composite types.
/// </summary>
/// <param name="Renames">The declared composite type renames.</param>
/// <param name="Drops">The composite types explicitly declared dropped.</param>
public sealed record CompositeTypeDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared composite type renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The composite types explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
