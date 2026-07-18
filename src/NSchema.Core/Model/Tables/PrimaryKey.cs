using System.Diagnostics;

namespace NSchema.Model.Tables;

/// <summary>
/// Represents a primary key constraint in a database schema.
/// </summary>
/// <param name="name">The name of the primary key constraint.</param>
/// <param name="columnNames">A list of column names that are part of the primary key constraint.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class PrimaryKey(SqlIdentifier name, List<SqlIdentifier> columnNames) : DatabaseMember(name), IEquatable<PrimaryKey>
{
    /// <summary>
    /// A list of column names that are part of the primary key constraint.
    /// </summary>
    public List<SqlIdentifier> ColumnNames { get; } = columnNames ?? [];

    /// <inheritdoc/>
    public override PrimaryKey Clone() => new(Name, [.. ColumnNames]) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(PrimaryKey? other) =>
        other is not null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PrimaryKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames.Count);

    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})";
}
