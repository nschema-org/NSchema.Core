namespace NSchema.Project.Model.Directives;

/// <summary>
/// The management directives declared for schemas.
/// </summary>
/// <param name="Renames">The declared schema renames.</param>
/// <param name="Drops">The schemas explicitly declared dropped.</param>
/// <param name="Partials">The schemas whose declarations are partial.</param>
public sealed record SchemaDirectives(
    IReadOnlyList<SchemaRenameDirective>? Renames = null,
    IReadOnlyList<SchemaDropDirective>? Drops = null,
    IReadOnlyList<SchemaPartialDirective>? Partials = null
)
{
    /// <summary>
    /// The declared schema renames.
    /// </summary>
    public IReadOnlyList<SchemaRenameDirective> Renames { get; init; } = Renames ?? [];

    /// <summary>
    /// The schemas explicitly declared dropped.
    /// </summary>
    public IReadOnlyList<SchemaDropDirective> Drops { get; init; } = Drops ?? [];

    /// <summary>
    /// The schemas whose declarations are partial.
    /// </summary>
    public IReadOnlyList<SchemaPartialDirective> Partials { get; init; } = Partials ?? [];
}
