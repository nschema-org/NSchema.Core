using System.Diagnostics;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Triggers;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a database table. Adopts its members.
/// </summary>
[DebuggerDisplay("{Name,nq} ({Columns.Count} columns)")]
public sealed class Table : DatabaseObject, IEquatable<Table>
{
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
    public DatabaseMemberCollection<Column> Columns
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of foreign keys that define the relationships between this table and other tables in the database schema.
    /// </summary>
    public DatabaseMemberCollection<ForeignKey> ForeignKeys
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of unique constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<UniqueConstraint> UniqueConstraints
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of check constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<CheckConstraint> CheckConstraints
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of exclusion constraints defined on the table.
    /// </summary>
    public DatabaseMemberCollection<ExclusionConstraint> ExclusionConstraints
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of indexes that are defined on the table.
    /// </summary>
    public DatabaseMemberCollection<TableIndex> Indexes
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <summary>
    /// A list of grants that define the permissions associated with the table.
    /// </summary>
    public List<TableGrant> Grants { get; init; } = [];

    /// <summary>
    /// A list of triggers defined on the table.
    /// </summary>
    public DatabaseMemberCollection<Trigger> Triggers
    {
        get => field ??= new(this);
        init { value.Attach(this); field = value; }
    }

    /// <inheritdoc/>
    public override Table Clone() => new()
    {
        Name = Name,
        PrimaryKey = PrimaryKey?.Clone(),
        Columns = [.. Columns.Select(c => c.Clone())],
        ForeignKeys = [.. ForeignKeys.Select(k => k.Clone())],
        UniqueConstraints = [.. UniqueConstraints.Select(u => u.Clone())],
        CheckConstraints = [.. CheckConstraints.Select(c => c.Clone())],
        ExclusionConstraints = [.. ExclusionConstraints.Select(x => x.Clone())],
        Indexes = [.. Indexes.Select(i => i.Clone())],
        Grants = [.. Grants],
        Triggers = [.. Triggers.Select(t => t.Clone())],
        Comment = Comment,
    };

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
