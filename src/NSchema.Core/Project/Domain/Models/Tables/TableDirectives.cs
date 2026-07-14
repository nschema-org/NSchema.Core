namespace NSchema.Project.Domain.Models.Tables;

/// <summary>
/// The management directives declared for tables.
/// </summary>
/// <param name="Renames">The declared table renames.</param>
/// <param name="Drops">The tables explicitly declared dropped.</param>
/// <param name="ColumnRenames">The declared column renames.</param>
public sealed record TableDirectives(
    IReadOnlyList<ObjectRename>? Renames = null,
    IReadOnlyList<ObjectReference>? Drops = null,
    IReadOnlyList<MemberRename>? ColumnRenames = null
)
{
    /// <summary>
    /// The declared table renames.
    /// </summary>
    public IReadOnlyList<ObjectRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The tables explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<ObjectReference> Drops { get; init; } = Drops ?? [];

    /// <summary>
    /// The declared column renames.
    /// </summary>
    public IReadOnlyList<MemberRename> ColumnRenames { get; init; } = ColumnRenames ?? [];
}
