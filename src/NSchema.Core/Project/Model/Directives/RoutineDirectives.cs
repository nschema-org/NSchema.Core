using NSchema.Model;

namespace NSchema.Project.Domain.Models;

/// <summary>
/// The management directives declared for routines.
/// </summary>
/// <param name="Renames">The declared routine renames.</param>
/// <param name="Drops">The routines explicitly declared dropped.</param>
public sealed record RoutineDirectives(
    IReadOnlyList<ObjectRenameDirective>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared routine renames.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The routines explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
