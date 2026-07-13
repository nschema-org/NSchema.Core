using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Constraints;

/// <summary>
/// Represents a unique constraint in a database schema.
/// </summary>
/// <param name="Name">The name of the unique constraint.</param>
/// <param name="ColumnNames">A list of column names that are part of the unique constraint.</param>
/// <param name="Comment">An optional comment or description for the constraint.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record UniqueConstraint(SqlIdentifier Name, IReadOnlyList<SqlIdentifier> ColumnNames, string? Comment = null) : INamedObject
{
    /// <summary>
    /// A list of column names that are part of the unique constraint.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> ColumnNames { get; init; } = ColumnNames ?? [];

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
