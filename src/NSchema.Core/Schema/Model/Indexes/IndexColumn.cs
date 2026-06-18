namespace NSchema.Schema.Model.Indexes;

/// <summary>
/// A single key of an index: either a column name or an expression, with optional sort direction and null ordering.
/// </summary>
/// <param name="Expression">The column name, or — when <paramref name="IsExpression"/> is set — the raw key expression.</param>
/// <param name="IsExpression">When <see langword="true"/>, <paramref name="Expression"/> is an opaque expression and is rendered parenthesised.</param>
/// <param name="Sort">The sort direction; <see cref="IndexSort.Default"/> leaves it to the database default.</param>
/// <param name="Nulls">Where nulls sort; <see cref="IndexNulls.Default"/> leaves it to the database default.</param>
public sealed record IndexColumn(
    string Expression,
    bool IsExpression = false,
    IndexSort Sort = IndexSort.Default,
    IndexNulls Nulls = IndexNulls.Default
)
{
    /// <summary>
    /// A bare column name converts to a plain ascending index key with default null ordering, so the common case
    /// — a list of column names — can be written directly (e.g. <c>["id", "email"]</c>).
    /// </summary>
    public static implicit operator IndexColumn(string column) => new(column);
}
