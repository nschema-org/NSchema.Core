using NSchema.Model;
namespace NSchema.Project.Domain.Models.Enums;

/// <summary>
/// The management directives declared for enum types.
/// </summary>
/// <param name="Renames">The declared enum type renames.</param>
/// <param name="Drops">The enum types explicitly declared dropped.</param>
public sealed record EnumDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared enum type renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The enum types explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
