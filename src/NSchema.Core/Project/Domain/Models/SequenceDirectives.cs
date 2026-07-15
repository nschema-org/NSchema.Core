using NSchema.Model;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives declared for sequences.
/// </summary>
/// <param name="Renames">The declared sequence renames.</param>
/// <param name="Drops">The sequences explicitly declared dropped.</param>
public sealed record SequenceDirectives(
    IReadOnlyList<ObjectRenameDirective>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared sequence renames.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The sequences explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
