namespace NSchema.Project.Domain.Models.Schemas;

/// <summary>
/// The management directives declared for schemas.
/// </summary>
/// <param name="Renames">The declared schema renames.</param>
/// <param name="Drops">The schemas explicitly declared dropped.</param>
/// <param name="Partials">The schemas whose declarations are partial.</param>
public sealed record SchemaDirectives(
    IReadOnlyList<SchemaRename>? Renames = null,
    IReadOnlyList<SqlIdentifier>? Drops = null,
    IReadOnlyList<SqlIdentifier>? Partials = null
)
{
    /// <summary>
    /// The declared schema renames.
    /// </summary>
    public IReadOnlyList<SchemaRename> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The schemas explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Drops { get; init; } = Drops ?? [];

    /// <summary>
    /// The schemas whose declarations are partial.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Partials { get; init; } = Partials ?? [];
}
