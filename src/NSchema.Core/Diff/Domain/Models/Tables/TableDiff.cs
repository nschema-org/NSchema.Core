using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Diff.Domain.Models.Triggers;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Diff.Domain.Models.Tables;

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
/// <param name="ExclusionConstraints">Exclusion constraint changes on the table.</param>
/// <param name="Triggers">Trigger changes on the table.</param>
/// <param name="Definition">
/// The full table definition when the table is being created (<see cref="ChangeKind.Add"/>); otherwise <see langword="null"/>.
/// </param>
public sealed record TableDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<ColumnDiff>? Columns = null,
    IReadOnlyList<GrantChange>? Grants = null,
    IReadOnlyList<IndexDiff>? Indexes = null,
    IReadOnlyList<PrimaryKeyDiff>? PrimaryKey = null,
    IReadOnlyList<ForeignKeyDiff>? ForeignKeys = null,
    IReadOnlyList<UniqueConstraintDiff>? UniqueConstraints = null,
    IReadOnlyList<CheckConstraintDiff>? Checks = null,
    IReadOnlyList<ExclusionConstraintDiff>? ExclusionConstraints = null,
    IReadOnlyList<TriggerDiff>? Triggers = null,
    Table? Definition = null
) : ISchemaObjectDiff
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

    /// <summary>
    /// Exclusion constraint changes on the table.
    /// </summary>
    public IReadOnlyList<ExclusionConstraintDiff> ExclusionConstraints { get; init; } = ExclusionConstraints ?? [];

    /// <summary>
    /// Trigger changes on the table.
    /// </summary>
    public IReadOnlyList<TriggerDiff> Triggers { get; init; } = Triggers ?? [];

    /// <summary>
    /// Enumerates every changed member of this table across all kinds (columns, indexes, constraints, triggers),
    /// for kind-agnostic consumers. A method rather than a property so serializers and snapshot tooling do not
    /// duplicate the per-kind collections. Grants are not members (they are keyed by role, not name).
    /// </summary>
    public IEnumerable<INamedObjectDiff> EnumerateMembers() =>
        Columns.Cast<INamedObjectDiff>()
            .Concat(Indexes)
            .Concat(PrimaryKey)
            .Concat(ForeignKeys)
            .Concat(UniqueConstraints)
            .Concat(Checks)
            .Concat(ExclusionConstraints)
            .Concat(Triggers);
}
