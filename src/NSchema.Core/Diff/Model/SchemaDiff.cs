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
/// <param name="Enums">The changed enum types within this schema, ordered by name.</param>
/// <param name="Sequences">The changed sequences within this schema, ordered by name.</param>
/// <param name="Routines">The changed routines (functions and procedures) within this schema, ordered by name.</param>
/// <param name="Domains">The changed domains within this schema, ordered by name.</param>
/// <param name="CompositeTypes">The changed composite types within this schema, ordered by name.</param>
public sealed record SchemaDiff(
    string Name,
    ChangeKind? Kind = null,
    string? RenamedFrom = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<GrantChange>? Grants = null,
    IReadOnlyList<TableDiff>? Tables = null,
    IReadOnlyList<ViewDiff>? Views = null,
    IReadOnlyList<EnumDiff>? Enums = null,
    IReadOnlyList<SequenceDiff>? Sequences = null,
    IReadOnlyList<RoutineDiff>? Routines = null,
    IReadOnlyList<DomainDiff>? Domains = null,
    IReadOnlyList<CompositeTypeDiff>? CompositeTypes = null
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

    /// <summary>
    /// The changed enum types within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<EnumDiff> Enums { get; init; } = Enums ?? [];

    /// <summary>
    /// The changed sequences within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<SequenceDiff> Sequences { get; init; } = Sequences ?? [];

    /// <summary>
    /// The changed routines (functions and procedures) within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<RoutineDiff> Routines { get; init; } = Routines ?? [];

    /// <summary>
    /// The changed domains within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<DomainDiff> Domains { get; init; } = Domains ?? [];

    /// <summary>
    /// The changed composite types within this schema, ordered by name.
    /// </summary>
    public IReadOnlyList<CompositeTypeDiff> CompositeTypes { get; init; } = CompositeTypes ?? [];

    /// <summary>
    /// Enumerates every changed object in this schema across all kinds, for kind-agnostic consumers (change
    /// summaries, destructive-change detection). A method rather than a property so serializers and snapshot
    /// tooling do not duplicate the per-kind collections.
    /// </summary>
    public IEnumerable<ISchemaObjectDiff> EnumerateObjects() =>
        Tables.Cast<ISchemaObjectDiff>()
            .Concat(Views)
            .Concat(Enums)
            .Concat(Sequences)
            .Concat(Routines)
            .Concat(Domains)
            .Concat(CompositeTypes);
}
