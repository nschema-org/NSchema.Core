using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a unique constraint in a database schema.
/// </summary>
/// <param name="Name">The name of the unique constraint.</param>
/// <param name="ColumnNames">A list of column names that are part of the unique constraint.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record UniqueConstraint(string Name, IReadOnlyList<string> ColumnNames)
{
    /// <summary>
    /// A list of column names that are part of the unique constraint.
    /// </summary>
    public IReadOnlyList<string> ColumnNames { get; init; } = ColumnNames ?? [];

    private string DebuggerDisplay => $"{Name}: ({string.Join(", ", ColumnNames)})";

    /// <summary>
    /// Determines whether the specified UniqueConstraint is equal to the current UniqueConstraint.
    /// </summary>
    /// <param name="other">The UniqueConstraint to compare with the current UniqueConstraint.</param>
    /// <returns>true if the specified UniqueConstraint is equal to the current UniqueConstraint; otherwise, false.</returns>
    public virtual bool Equals(UniqueConstraint? other) =>
        other != null
        && Name == other.Name
        && ColumnNames.SequenceEqual(other.ColumnNames);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, ColumnNames);
}
