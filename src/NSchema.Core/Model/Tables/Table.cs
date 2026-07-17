using System.Diagnostics;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Triggers;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a database table.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public sealed class Table : DatabaseObject, IEquatable<Table>
{
    /// <summary>
    /// Creates a table, adopting the given members.
    /// </summary>
    /// <param name="name">The name of the table.</param>
    /// <param name="primaryKey">The primary key of the table.</param>
    /// <param name="columns">A list of columns that are part of the table.</param>
    /// <param name="foreignKeys">A list of foreign keys that define the relationships between this table and other tables in the database schema.</param>
    /// <param name="uniqueConstraints">A list of unique constraints defined on the table.</param>
    /// <param name="checkConstraints">A list of check constraints defined on the table.</param>
    /// <param name="exclusionConstraints">A list of exclusion constraints defined on the table.</param>
    /// <param name="indexes">A list of indexes that are defined on the table.</param>
    /// <param name="grants">A list of grants that define the permissions associated with the table.</param>
    /// <param name="triggers">A list of triggers defined on the table.</param>
    public Table(
        SqlIdentifier name,
        PrimaryKey? primaryKey = null,
        IReadOnlyList<Column>? columns = null,
        IReadOnlyList<ForeignKey>? foreignKeys = null,
        IReadOnlyList<UniqueConstraint>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraint>? checkConstraints = null,
        IReadOnlyList<ExclusionConstraint>? exclusionConstraints = null,
        IReadOnlyList<TableIndex>? indexes = null,
        IReadOnlyList<TableGrant>? grants = null,
        IReadOnlyList<Trigger>? triggers = null
    ) : base(name)
    {
        PrimaryKey = primaryKey;
        Columns = columns ?? [];
        ForeignKeys = foreignKeys ?? [];
        UniqueConstraints = uniqueConstraints ?? [];
        CheckConstraints = checkConstraints ?? [];
        ExclusionConstraints = exclusionConstraints ?? [];
        Indexes = indexes ?? [];
        Grants = grants ?? [];
        Triggers = triggers ?? [];
    }

    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Table;

    /// <summary>
    /// The primary key of the table.
    /// </summary>
    public PrimaryKey? PrimaryKey
    {
        get;
        init
        {
            value?.Parent = this;
            field = value;
        }
    }

    /// <summary>
    /// A list of columns that are part of the table.
    /// </summary>
    public IReadOnlyList<Column> Columns { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public IReadOnlyList<ForeignKey> ForeignKeys { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of unique constraints defined on the table.
    /// </summary>
    public IReadOnlyList<UniqueConstraint> UniqueConstraints { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of check constraints defined on the table.
    /// </summary>
    public IReadOnlyList<CheckConstraint> CheckConstraints { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of exclusion constraints defined on the table.
    /// </summary>
    public IReadOnlyList<ExclusionConstraint> ExclusionConstraints { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of indexes that are defined on the table.
    /// </summary>
    public IReadOnlyList<TableIndex> Indexes { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// A list of grants that define the permissions associated with the table.
    /// </summary>
    public IReadOnlyList<TableGrant> Grants { get; init; }

    /// <summary>
    /// A list of triggers defined on the table.
    /// </summary>
    public IReadOnlyList<Trigger> Triggers { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// Returns a copy of the table with the given members replaced, outside any tree. A <see langword="null"/>
    /// argument keeps the current members.
    /// </summary>
    public Table With(
        IReadOnlyList<Column>? columns = null,
        PrimaryKey? primaryKey = null,
        IReadOnlyList<ForeignKey>? foreignKeys = null,
        IReadOnlyList<UniqueConstraint>? uniqueConstraints = null,
        IReadOnlyList<CheckConstraint>? checkConstraints = null,
        IReadOnlyList<ExclusionConstraint>? exclusionConstraints = null,
        IReadOnlyList<TableIndex>? indexes = null,
        IReadOnlyList<TableGrant>? grants = null,
        IReadOnlyList<Trigger>? triggers = null) =>
        new(Name, (primaryKey ?? PrimaryKey)?.Clone(),
            [.. (columns ?? Columns).Select(c => c.Clone())],
            [.. (foreignKeys ?? ForeignKeys).Select(k => k.Clone())],
            [.. (uniqueConstraints ?? UniqueConstraints).Select(u => u.Clone())],
            [.. (checkConstraints ?? CheckConstraints).Select(c => c.Clone())],
            [.. (exclusionConstraints ?? ExclusionConstraints).Select(x => x.Clone())],
            [.. (indexes ?? Indexes).Select(i => i.Clone())],
            grants ?? Grants,
            [.. (triggers ?? Triggers).Select(t => t.Clone())]) { Comment = Comment };

    internal Table Clone() => With();

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(Table? other) =>
        other is not null
        && Name == other.Name
        && Equals(PrimaryKey, other.PrimaryKey)
        && Columns.SequenceEqual(other.Columns)
        && ForeignKeys.SequenceEqual(other.ForeignKeys)
        && UniqueConstraints.SequenceEqual(other.UniqueConstraints)
        && CheckConstraints.SequenceEqual(other.CheckConstraints)
        && ExclusionConstraints.SequenceEqual(other.ExclusionConstraints)
        && Indexes.SequenceEqual(other.Indexes)
        && Grants.SequenceEqual(other.Grants)
        && Triggers.SequenceEqual(other.Triggers);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Table other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Columns.Count);
}
