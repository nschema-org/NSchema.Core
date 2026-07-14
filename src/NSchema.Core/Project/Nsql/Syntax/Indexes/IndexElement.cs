using NSchema.Project.Domain.Models;

namespace NSchema.Project.Nsql.Syntax.Indexes;

/// <summary>
/// A single index key: a column name or a raw expression, with its ordering.
/// </summary>
/// <param name="Column">The column name; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Expression">The raw key expression; exactly one of <paramref name="Column"/> and <paramref name="Expression"/> is given.</param>
/// <param name="Sort">The sort direction; <see cref="IndexSort.Default"/> when unwritten.</param>
/// <param name="Nulls">Where nulls sort; <see cref="IndexNulls.Default"/> when unwritten.</param>
public sealed record IndexElement(
    Identifier? Column = null,
    SqlText? Expression = null,
    IndexSort Sort = IndexSort.Default,
    IndexNulls Nulls = IndexNulls.Default
) : NsqlNode;
