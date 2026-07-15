using NSchema.Model;
namespace NSchema.Project.Domain.Models.Views;

/// <summary>
/// The management directives declared for views.
/// </summary>
/// <param name="Renames">The declared view renames.</param>
/// <param name="Drops">The views explicitly declared dropped.</param>
public sealed record ViewDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared view renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The views explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
