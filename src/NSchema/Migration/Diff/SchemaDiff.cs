namespace NSchema.Migration.Diff;

/// <summary>
/// Describes the changes affecting a single schema and the tables within it.
/// </summary>
/// <param name="Name">The schema name.</param>
/// <param name="Kind">
/// The change to the schema entity itself, or <see langword="null"/> when the schema is unchanged
/// and only its <paramref name="Tables"/> have changes.
/// </param>
/// <param name="RenamedFrom">The previous schema name when the schema is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the schema's comment, if any.</param>
/// <param name="Grants">Usage grants and revocations on the schema.</param>
/// <param name="Tables">The changed tables within this schema, ordered by name.</param>
public sealed record SchemaDiff(
    string Name,
    ChangeKind? Kind,
    string? RenamedFrom,
    ValueChange<string>? Comment,
    IReadOnlyList<GrantChange> Grants,
    IReadOnlyList<TableDiff> Tables);
