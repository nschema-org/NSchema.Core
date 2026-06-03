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
/// <param name="Constraints">Primary and foreign key changes on the table.</param>
/// <param name="Definition">
/// The full table definition when the table is being created (<see cref="ChangeKind.Add"/>); otherwise
/// <see langword="null"/>. Carries the primary key and column definitions needed to emit a single
/// <c>CREATE TABLE</c>, so the linearizer can reconstruct the create action without re-deriving them.
/// </param>
public sealed record TableDiff(
    string Schema,
    string Name,
    ChangeKind Kind,
    string? RenamedFrom,
    ValueChange<string>? Comment,
    IReadOnlyList<ColumnDiff> Columns,
    IReadOnlyList<GrantChange> Grants,
    IReadOnlyList<IndexDiff> Indexes,
    IReadOnlyList<ConstraintDiff> Constraints,
    Table? Definition = null);
