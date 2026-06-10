namespace NSchema.Diff.Model;

/// <summary>
/// Describes the changes in a given schema and its tables.
/// </summary>
/// <param name="Name">The schema name.</param>
/// <param name="Kind">The change to the schema entity itself, or <see langword="null"/> when only its children have changes.</param>
/// <param name="RenamedFrom">The previous schema name when the schema is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the schema's comment, if any.</param>
/// <param name="Grants">Usage grants and revocations on the schema.</param>
/// <param name="Tables">The changed tables within this schema, ordered by name.</param>
/// <param name="Views">The changed views within this schema, ordered by name.</param>
public sealed record SchemaDiff(
    string Name,
    ChangeKind? Kind = null,
    string? RenamedFrom = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<GrantChange>? Grants = null,
    IReadOnlyList<TableDiff>? Tables = null,
    IReadOnlyList<ViewDiff>? Views = null
)
{
    /// <summary>
    /// Usage grants and revocations on the schema.
    /// </summary>
    public IReadOnlyList<GrantChange> Grants { get; init; } = Grants ?? [];

    /// <summary>
    /// The changed tables within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<TableDiff> Tables { get; init; } = Tables ?? [];

    /// <summary>
    /// The changed views within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<ViewDiff> Views { get; init; } = Views ?? [];
}
