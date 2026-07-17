using System.Diagnostics;

namespace NSchema.Model.Constraints;

/// <summary>
/// Represents a check constraint in a database schema.
/// </summary>
/// <param name="name">The name of the check constraint.</param>
/// <param name="expression">The SQL boolean expression the constraint enforces.</param>
[DebuggerDisplay("{Name,nq}: {Expression,nq}")]
public sealed class CheckConstraint(SqlIdentifier name, SqlText expression) : DatabaseMember(name), IEquatable<CheckConstraint>
{
    /// <summary>
    /// The SQL boolean expression the constraint enforces.
    /// </summary>
    public SqlText Expression { get; init; } = expression;

    internal CheckConstraint Clone() => new(Name, Expression) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(CheckConstraint? other) =>
        other is not null
        && Name == other.Name
        && Expression == other.Expression;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is CheckConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Expression);
}
