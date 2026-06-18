namespace NSchema.Schema.Model.Constraints;

/// <summary>
/// A single element of an exclusion constraint: a column or expression paired with the operator that must not
/// hold simultaneously across two rows (e.g. <c>during WITH &amp;&amp;</c>, <c>room WITH =</c>).
/// </summary>
/// <param name="Expression">The column name, or — when <paramref name="IsExpression"/> is set — the raw element expression.</param>
/// <param name="Operator">The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).</param>
/// <param name="IsExpression">When <see langword="true"/>, <paramref name="Expression"/> is an opaque expression and is rendered parenthesised.</param>
public sealed record ExclusionElement(string Expression, string Operator, bool IsExpression = false);
