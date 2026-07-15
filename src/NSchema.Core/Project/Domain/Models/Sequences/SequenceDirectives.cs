namespace NSchema.Project.Domain.Models.Sequences;

/// <summary>
/// The management directives declared for sequences.
/// </summary>
/// <param name="Renames">The declared sequence renames.</param>
/// <param name="Drops">The sequences explicitly declared dropped.</param>
public sealed record SequenceDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared sequence renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The sequences explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
