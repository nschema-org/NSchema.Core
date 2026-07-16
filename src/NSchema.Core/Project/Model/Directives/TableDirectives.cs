using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// The management directives declared for tables.
/// </summary>
/// <param name="Renames">The declared table renames.</param>
/// <param name="Drops">The tables explicitly declared dropped.</param>
/// <param name="ColumnRenames">The declared column renames.</param>
/// <param name="ChangeScripts">The change-event scripts targeting this schema's table members.</param>
public sealed record TableDirectives(
    IReadOnlyList<ObjectRenameDirective>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null,
    IReadOnlyList<MemberRenameDirective>? ColumnRenames = null,
    IReadOnlyList<ChangeScript>? ChangeScripts = null
)
{
    /// <summary>
    /// The declared table renames.
    /// </summary>
    public IReadOnlyList<ObjectRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The tables explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];

    /// <summary>
    /// The declared column renames.
    /// </summary>
    public IReadOnlyList<MemberRenameDirective> ColumnRenames { get; init; } = ColumnRenames ?? [];

    /// <summary>
    /// The change-event scripts targeting this schema's table members.
    /// </summary>
    public IReadOnlyList<ChangeScript> ChangeScripts { get; init; } = ChangeScripts ?? [];
}
