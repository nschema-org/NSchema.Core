using System.Diagnostics;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Triggers;

namespace NSchema.Schema.Model.Tables;

/// <summary>
/// Represents a database table.
/// </summary>
/// <param name="Name">The name of the table.</param>
/// <param name="OldName">The previous name of the table, if it has been renamed.</param>
/// <param name="PrimaryKey">The primary key of the table.</param>
/// <param name="Comment">An optional comment or description for the table.</param>
/// <param name="Columns">A list of columns that are part of the table.</param>
/// <param name="ForeignKeys">A list of foreign keys that define the relationships between this table and other tables in the database schema.</param>
/// <param name="UniqueConstraints">A list of unique constraints defined on the table.</param>
/// <param name="CheckConstraints">A list of check constraints defined on the table.</param>
/// <param name="Indexes">A list of indexes that are defined on the table.</param>
/// <param name="Grants">A list of grants that define the permissions associated with the table.</param>
/// <param name="Triggers">A list of triggers defined on the table.</param>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public record Table(
    string Name,
    string? OldName = null,
    PrimaryKey? PrimaryKey = null,
    string? Comment = null,
    IReadOnlyList<Column>? Columns = null,
    IReadOnlyList<ForeignKey>? ForeignKeys = null,
    IReadOnlyList<UniqueConstraint>? UniqueConstraints = null,
    IReadOnlyList<CheckConstraint>? CheckConstraints = null,
    IReadOnlyList<TableIndex>? Indexes = null,
    IReadOnlyList<TableGrant>? Grants = null,
    IReadOnlyList<Trigger>? Triggers = null
) : IRenameableObject
{
    /// <summary>
    /// A list of columns that are part of the table.
    /// </summary>
    public IReadOnlyList<Column> Columns { get; init; } = Columns ?? [];

    /// <summary>
    /// A list of foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public IReadOnlyList<ForeignKey> ForeignKeys { get; init; } = ForeignKeys ?? [];

    /// <summary>
    /// A list of unique constraints defined on the table.
    /// </summary>
    public IReadOnlyList<UniqueConstraint> UniqueConstraints { get; init; } = UniqueConstraints ?? [];

    /// <summary>
    /// A list of check constraints defined on the table.
    /// </summary>
    public IReadOnlyList<CheckConstraint> CheckConstraints { get; init; } = CheckConstraints ?? [];

    /// <summary>
    /// A list of indexes that are defined on the table.
    /// </summary>
    public IReadOnlyList<TableIndex> Indexes { get; init; } = Indexes ?? [];

    /// <summary>
    /// A list of grants that define the permissions associated with the table.
    /// </summary>
    public IReadOnlyList<TableGrant> Grants { get; init; } = Grants ?? [];

    /// <summary>
    /// A list of triggers defined on the table.
    /// </summary>
    public IReadOnlyList<Trigger> Triggers { get; init; } = Triggers ?? [];
}
