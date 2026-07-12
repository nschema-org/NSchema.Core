using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Constraints;

/// <summary>
/// Represents an exclusion constraint: it guarantees that for any two rows, the given operators do not all return
/// true across the listed elements (e.g. <c>EXCLUDE USING gist (room WITH =, during WITH &amp;&amp;)</c> forbids
/// overlapping bookings of the same room).
/// </summary>
/// <param name="Name">The name of the exclusion constraint.</param>
/// <param name="Elements">The constrained elements, each a column or expression paired with an operator.</param>
/// <param name="Method">The access method backing the constraint (e.g. <c>gist</c>); <see langword="null"/> means the database default.</param>
/// <param name="Predicate">An optional predicate restricting the constraint to a subset of rows (a partial constraint).</param>
/// <param name="Comment">An optional comment or description for the constraint.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record ExclusionConstraint(
    string Name,
    IReadOnlyList<ExclusionElement> Elements,
    string? Method = null,
    string? Predicate = null,
    string? Comment = null
) : INamedObject
{
    /// <summary>
    /// The constrained elements, each a column or expression paired with an operator.
    /// </summary>
    public IReadOnlyList<ExclusionElement> Elements { get; init; } = Elements ?? [];

    private string DebuggerDisplay =>
        $"{Name}: EXCLUDE" +
        (Method is { } m ? $" USING {m}" : "") +
        $" ({string.Join(", ", Elements.Select(e => $"{e.Expression} WITH {e.Operator}"))})";

    /// <summary>
    /// Determines whether the specified ExclusionConstraint is structurally equal to the current one (excluding the comment).
    /// </summary>
    /// <param name="other">The ExclusionConstraint to compare with the current one.</param>
    /// <returns>true if the specified ExclusionConstraint is structurally equal; otherwise, false.</returns>
    public virtual bool Equals(ExclusionConstraint? other) =>
        other != null
        && Name == other.Name
        && Method == other.Method
        && Predicate == other.Predicate
        && Elements.SequenceEqual(other.Elements);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Method, Predicate, Elements.Count);
}
