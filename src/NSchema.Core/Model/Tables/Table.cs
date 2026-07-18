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
        DatabaseMemberCollection<Column>? columns = null,
        DatabaseMemberCollection<ForeignKey>? foreignKeys = null,
        DatabaseMemberCollection<UniqueConstraint>? uniqueConstraints = null,
        DatabaseMemberCollection<CheckConstraint>? checkConstraints = null,
        DatabaseMemberCollection<ExclusionConstraint>? exclusionConstraints = null,
        DatabaseMemberCollection<TableIndex>? indexes = null,
        List<TableGrant>? grants = null,
        DatabaseMemberCollection<Trigger>? triggers = null
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
        Columns.Attach(this);
        ForeignKeys.Attach(this);
        UniqueConstraints.Attach(this);
        CheckConstraints.Attach(this);
        ExclusionConstraints.Attach(this);
        Indexes.Attach(this);
        Triggers.Attach(this);
    }

    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Table;

    /// <summary>
    /// The primary key of the table.
    /// </summary>
    public PrimaryKey? PrimaryKey
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }
            value?.Parent = this;
            field?.Parent = null;
            field = value;
        }
    }

    /// <summary>
    /// A list of columns that are part of the table.
    /// </summary>
    public DatabaseMemberCollection<Column> Columns { get; }

    /// <summary>
    /// A list of foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public DatabaseMemberCollection<ForeignKey> ForeignKeys { get; }

    /// <summary>
    /// A list of unique constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<UniqueConstraint> UniqueConstraints { get; }

    /// <summary>
    /// A list of check constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<CheckConstraint> CheckConstraints { get; }

    /// <summary>
    /// A list of exclusion constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<ExclusionConstraint> ExclusionConstraints { get; }

    /// <summary>
    /// A list of indexes that are defined on the table.
    /// </summary>
    public DatabaseMemberCollection<TableIndex> Indexes { get; }

    /// <summary>
    /// A list of grants that define the permissions associated with the table.
    /// </summary>
    public List<TableGrant> Grants { get; }

    /// <summary>
    /// A list of triggers defined on the table.
    /// </summary>
    public DatabaseMemberCollection<Trigger> Triggers { get; }

    /// <inheritdoc/>
    public override Table Clone() =>
        new(Name, PrimaryKey?.Clone(),
            [.. Columns.Select(c => c.Clone())],
            [.. ForeignKeys.Select(k => k.Clone())],
            [.. UniqueConstraints.Select(u => u.Clone())],
            [.. CheckConstraints.Select(c => c.Clone())],
            [.. ExclusionConstraints.Select(x => x.Clone())],
            [.. Indexes.Select(i => i.Clone())],
            [.. Grants],
            [.. Triggers.Select(t => t.Clone())])
        { Comment = Comment };

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
