using System.Diagnostics;
using NSchema.Model.Indexes;

namespace NSchema.Model.Views;

/// <summary>
/// Represents a database view: a named query stored in a schema.
/// </summary>
[DebuggerDisplay("{Name,nq} (view)")]
public sealed class View : DatabaseObject, IEquatable<View>
{
    /// <summary>
    /// Creates a view, adopting its indexes.
    /// </summary>
    /// <param name="name">The name of the view.</param>
    /// <param name="body">The view's defining query, stored verbatim (the text after <c>AS</c>).</param>
    /// <param name="dependsOn">The objects the view reads, derived from <paramref name="body"/>.</param>
    /// <param name="isMaterialized">Whether this is a materialized view (stores its result set).</param>
    /// <param name="indexes">Indexes on the view. Only materialized views carry indexes; empty for a plain view.</param>
    public View(
        SqlIdentifier name,
        SqlText body,
        IReadOnlyList<ViewDependency>? dependsOn = null,
        bool isMaterialized = false,
        IReadOnlyList<TableIndex>? indexes = null
    ) : base(name)
    {
        Body = body;
        DependsOn = dependsOn ?? [];
        IsMaterialized = isMaterialized;
        Indexes = indexes ?? [];
    }

    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.View;

    /// <summary>
    /// The view's defining query, stored verbatim (the text after <c>AS</c>).
    /// </summary>
    public SqlText Body { get; init; }

    /// <summary>
    /// The objects the view reads, derived from <see cref="Body"/>.
    /// </summary>
    public IReadOnlyList<ViewDependency> DependsOn { get; init; }

    /// <summary>
    /// Whether this is a materialized view (stores its result set).
    /// </summary>
    public bool IsMaterialized { get; init; }

    /// <summary>
    /// Indexes on the view (materialized views only; empty for a plain view).
    /// </summary>
    public IReadOnlyList<TableIndex> Indexes { get; init => field = value.ForEach(f => f.Parent = this); }

    /// <summary>
    /// Returns a copy of the view with the given indexes, outside any tree.
    /// </summary>
    public View With(IReadOnlyList<TableIndex> indexes) => new(Name, Body, DependsOn, IsMaterialized, [.. indexes.Select(i => i.Clone())]) { Comment = Comment };

    internal View Clone() => new(Name, Body, DependsOn, IsMaterialized, [.. Indexes.Select(i => i.Clone())]) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(View? other) =>
        other is not null
        && Name == other.Name
        && Body == other.Body
        && IsMaterialized == other.IsMaterialized
        && DependsOn.SequenceEqual(other.DependsOn)
        && Indexes.SequenceEqual(other.Indexes);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is View other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Body, IsMaterialized);
}
