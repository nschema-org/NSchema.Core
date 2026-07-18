using System.Diagnostics;

namespace NSchema.Model.Constraints;

/// <summary>
/// Represents an exclusion constraint: it guarantees that for any two rows, the given operators do not all return
/// true across the listed elements (e.g. <c>EXCLUDE USING gist (room WITH =, during WITH &amp;&amp;)</c> forbids
/// overlapping bookings of the same room).
/// </summary>
/// <param name="name">The name of the exclusion constraint.</param>
/// <param name="elements">The constrained elements, each a column or expression paired with an operator.</param>
/// <param name="method">The access method backing the constraint (e.g. <c>gist</c>); <see langword="null"/> means the database default.</param>
/// <param name="predicate">An optional predicate restricting the constraint to a subset of rows (a partial constraint).</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class ExclusionConstraint(
    SqlIdentifier name,
    IReadOnlyList<ExclusionElement> elements,
    string? method = null,
    SqlText? predicate = null
) : DatabaseMember(name), IEquatable<ExclusionConstraint>
{
    /// <summary>
    /// The constrained elements, each a column or expression paired with an operator.
    /// </summary>
    public IReadOnlyList<ExclusionElement> Elements { get; init; } = elements ?? [];

    /// <summary>
    /// The access method backing the constraint (e.g. <c>gist</c>); <see langword="null"/> means the database default.
    /// </summary>
    public string? Method { get; init; } = method;

    /// <summary>
    /// An optional predicate restricting the constraint to a subset of rows (a partial constraint).
    /// </summary>
    public SqlText? Predicate { get; init; } = predicate;

    internal ExclusionConstraint Clone() => new(Name, Elements, Method, Predicate) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition.
    /// </summary>
    public bool Equals(ExclusionConstraint? other) =>
        other is not null
        && Name == other.Name
        && Method == other.Method
        && Equals(Predicate, other.Predicate)
        && Elements.SequenceEqual(other.Elements);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is ExclusionConstraint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Method, Predicate, Elements.Count);

    private string DebuggerDisplay =>
        $"{Name}: EXCLUDE" +
        (Method is { } m ? $" USING {m}" : "") +
        $" ({string.Join(", ", Elements.Select(e => $"{e.Expression} WITH {e.Operator}"))})";
}
