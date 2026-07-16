using NSchema.Model;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// The management directives declared for enum types.
/// </summary>
/// <param name="Renames">The declared enum type renames.</param>
/// <param name="Drops">The enum types explicitly declared dropped.</param>
public sealed record EnumDirectives(
    IReadOnlyList<ObjectRenameDirective>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null
)
{
    /// <summary>
    /// The declared enum type renames.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The enum types explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];
}
