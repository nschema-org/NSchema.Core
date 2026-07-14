using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Constraints;

/// <summary>
/// A single element of an exclusion constraint: a column name or a raw expression, with its operator.
/// </summary>
/// <param name="Operator">The exclusion operator (e.g. <c>=</c>, <c>&amp;&amp;</c>).</param>
/// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Expression">The raw element expression; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
public sealed record ExclusionElement(string Operator, Identifier? Column = null, SqlText? Expression = null) : NsqlNode;
