using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a check constraint in a database schema.
/// </summary>
/// <param name="Name">The name of the check constraint.</param>
/// <param name="Expression">The SQL boolean expression the constraint enforces.</param>
/// <param name="Comment">An optional comment or description for the constraint.</param>
[DebuggerDisplay("{Name,nq}: {Expression,nq}")]
public record CheckConstraint(string Name, string Expression, string? Comment = null) : INamedObject
{
    /// <summary>
    /// Determines whether the specified CheckConstraint is structurally equal to the current one.
    /// </summary>
    /// <param name="other">The CheckConstraint to compare with the current CheckConstraint.</param>
    /// <returns>true if the specified CheckConstraint is structurally equal; otherwise, false.</returns>
    public virtual bool Equals(CheckConstraint? other) =>
        other != null
        && Name == other.Name
        && Expression == other.Expression;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Expression);
}
