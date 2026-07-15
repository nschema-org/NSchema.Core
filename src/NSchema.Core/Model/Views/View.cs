using System.Diagnostics;
using NSchema.Model.Indexes;

namespace NSchema.Model.Views;

/// <summary>
/// Represents a database view: a named query stored in a schema.
/// </summary>
/// <param name="Name">The name of the view.</param>
/// <param name="Body">The view's defining query, stored verbatim (the text after <c>AS</c>).</param>
/// <param name="Comment">An optional comment or description for the view.</param>
/// <param name="DependsOn">The objects the view reads, derived from <paramref name="Body"/>.</param>
/// <param name="IsMaterialized">Whether this is a materialized view (stores its result set).</param>
/// <param name="Indexes">Indexes on the view. Only materialized views carry indexes; empty for a plain view.</param>
[DebuggerDisplay("{Name,nq} (view)")]
public sealed record View(
    SqlIdentifier Name,
    SqlText Body,
    string? Comment = null,
    IReadOnlyList<ViewDependency>? DependsOn = null,
    bool IsMaterialized = false,
    IReadOnlyList<TableIndex>? Indexes = null
) : INamedObject
{
    /// <summary>
    /// The objects the view reads, derived from <see cref="Body"/>.
    /// </summary>
    public IReadOnlyList<ViewDependency> DependsOn { get; init; } = DependsOn ?? [];

    /// <summary>
    /// Indexes on the view (materialized views only; empty for a plain view).
    /// </summary>
    public IReadOnlyList<TableIndex> Indexes { get; init; } = Indexes ?? [];
}
