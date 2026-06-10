using NSchema.Schema.Model;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes the changes affecting a single table.
/// </summary>
/// <param name="Schema">The name of the schema the table belongs to.</param>
/// <param name="Name">The table name.</param>
/// <param name="Kind">The change to the table.</param>
/// <param name="RenamedFrom">The previous table name when the table is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the table's comment, if any.</param>
/// <param name="Columns">The changed columns, ordered as encountered in the plan.</param>
/// <param name="Grants">Privileges granted or revoked on the table.</param>
/// <param name="Indexes">Index changes on the table.</param>
/// <param name="PrimaryKey">Primary key changes on the table.</param>
/// <param name="ForeignKeys">Foreign key changes on the table.</param>
/// <param name="UniqueConstraints">Unique constraint changes on the table.</param>
/// <param name="Checks">Check constraint changes on the table.</param>
/// <param name="Definition">
/// The full table definition when the table is being created (<see cref="ChangeKind.Add"/>); otherwise <see langword="null"/>.
/// </param>
public sealed record TableDiff(
    string Schema,
    string Name,
    ChangeKind Kind,
    string? RenamedFrom = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<ColumnDiff>? Columns = null,
    IReadOnlyList<GrantChange>? Grants = null,
    IReadOnlyList<IndexDiff>? Indexes = null,
    IReadOnlyList<PrimaryKeyDiff>? PrimaryKey = null,
    IReadOnlyList<ForeignKeyDiff>? ForeignKeys = null,
    IReadOnlyList<UniqueConstraintDiff>? UniqueConstraints = null,
    IReadOnlyList<CheckConstraintDiff>? Checks = null,
    Table? Definition = null
)
{
    /// <summary>
    /// The changed columns, ordered as encountered in the plan.
    /// </summary>
    public IReadOnlyList<ColumnDiff> Columns { get; init; } = Columns ?? [];

    /// <summary>
    /// Privileges granted or revoked on the table.
    /// </summary>
    public IReadOnlyList<GrantChange> Grants { get; init; } = Grants ?? [];

    /// <summary>
    /// Index changes on the table.
    /// </summary>
    public IReadOnlyList<IndexDiff> Indexes { get; init; } = Indexes ?? [];

    /// <summary>
    /// Primary key changes on the table.
    /// </summary>
    public IReadOnlyList<PrimaryKeyDiff> PrimaryKey { get; init; } = PrimaryKey ?? [];

    /// <summary>
    /// Foreign key changes on the table.
    /// </summary>
    public IReadOnlyList<ForeignKeyDiff> ForeignKeys { get; init; } = ForeignKeys ?? [];

    /// <summary>
    /// Unique constraint changes on the table.
    /// </summary>
    public IReadOnlyList<UniqueConstraintDiff> UniqueConstraints { get; init; } = UniqueConstraints ?? [];

    /// <summary>
    /// Check constraint changes on the table.
    /// </summary>
    public IReadOnlyList<CheckConstraintDiff> Checks { get; init; } = Checks ?? [];
}
