using System.Diagnostics;

namespace NSchema.Model.Indexes;

/// <summary>
/// Represents an index on a table or materialized view within the database schema.
/// </summary>
/// <param name="name">The name of the index.</param>
/// <param name="columns">The index keys (columns or expressions) in order, each with optional sort/null ordering.</param>
/// <param name="isUnique">A boolean value indicating whether the index enforces uniqueness on the indexed columns.</param>
/// <param name="predicate">An optional predicate that defines a partial index.</param>
/// <param name="method">The access method (e.g. <c>gin</c>, <c>gist</c>, <c>brin</c>); <see langword="null"/> means the database default (B-tree).</param>
/// <param name="include">Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class TableIndex(
    SqlIdentifier name,
    IReadOnlyList<IndexColumn> columns,
    bool isUnique = false,
    SqlText? predicate = null,
    string? method = null,
    IReadOnlyList<SqlIdentifier>? include = null
) : DatabaseMember(name), IEquatable<TableIndex>
{
    /// <summary>
    /// The index keys (columns or expressions) in order.
    /// </summary>
    public IReadOnlyList<IndexColumn> Columns { get; init; } = columns ?? [];

    /// <summary>
    /// A boolean value indicating whether the index enforces uniqueness on the indexed columns.
    /// </summary>
    public bool IsUnique { get; init; } = isUnique;

    /// <summary>
    /// An optional predicate that defines a partial index.
    /// </summary>
    public SqlText? Predicate { get; init; } = predicate;

    /// <summary>
    /// The access method; <see langword="null"/> means the database default (B-tree).
    /// </summary>
    public string? Method { get; init; } = method;

    /// <summary>
    /// Non-key columns carried in the index leaf pages (a covering <c>INCLUDE</c> clause).
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Include { get; init; } = include ?? [];

    internal TableIndex Clone() => new(Name, Columns, IsUnique, Predicate, Method, Include) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the parent and the comment are excluded.
    /// </summary>
    public bool Equals(TableIndex? other) =>
        other is not null
        && Name == other.Name
        && IsUnique == other.IsUnique
        && Method == other.Method
        && Equals(Predicate, other.Predicate)
        && Columns.SequenceEqual(other.Columns)
        && Include.SequenceEqual(other.Include);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TableIndex other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, IsUnique, Method, Predicate, Columns.Count, Include.Count);

    private string DebuggerDisplay =>
        $"{Name}: ({string.Join(", ", Columns.Select(c => c.Expression))})" +
        (IsUnique ? " UNIQUE" : "") +
        (Method is { } m ? $" USING {m}" : "") +
        (Predicate is { } p ? $" WHERE {p}" : "");
}
