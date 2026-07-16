using System.Diagnostics;

namespace NSchema.Model.Indexes;

/// <summary>
/// Represents an index on a table within the database schema.
/// </summary>
/// <param name="Name">The name of the index.</param>
/// <param name="Columns">The index keys (columns or expressions) in order, each with optional sort/null ordering.</param>
/// <param name="IsUnique">A boolean value indicating whether the index enforces uniqueness on the indexed columns.</param>
/// <param name="Comment">An optional comment or description for the index.</param>
/// <param name="Predicate">An optional predicate that defines a partial index.</param>
/// <param name="Method">The access method (e.g. <c>gin</c>, <c>gist</c>, <c>brin</c>); <see langword="null"/> means the database default (B-tree).</param>
/// <param name="Include">Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record TableIndex(
    SqlIdentifier Name,
    IReadOnlyList<IndexColumn> Columns,
    bool IsUnique = false,
    string? Comment = null,
    SqlText? Predicate = null,
    string? Method = null,
    IReadOnlyList<SqlIdentifier>? Include = null
) : INamedObject
{
    /// <summary>
    /// The index keys (columns or expressions) in order.
    /// </summary>
    public IReadOnlyList<IndexColumn> Columns { get; init; } = Columns ?? [];

    /// <summary>
    /// Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Include { get; init; } = Include ?? [];

    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", Columns.Select(c => c.Expression))})" +
        (IsUnique ? " UNIQUE" : "") +
        (Method is { } m ? $" USING {m}" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");

    /// <summary>
    /// Determines whether the specified TableIndex is structurally equal to the current one (excluding the comment).
    /// </summary>
    /// <param name="other">The TableIndex to compare with the current TableIndex.</param>
    /// <returns>true if the specified TableIndex is structurally equal; otherwise, false.</returns>
    public virtual bool Equals(TableIndex? other) =>
        other != null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && Method == other.Method
        && Predicate == other.Predicate
        && Columns.SequenceEqual(other.Columns)
        && Include.SequenceEqual(other.Include);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, IsUnique, Method, Predicate, Columns.Count, Include.Count);
}
