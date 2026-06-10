using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a database view: a named query stored in a schema.
/// </summary>
/// <param name="Name">The name of the view.</param>
/// <param name="Body">The view's defining query, stored verbatim (the text after <c>AS</c>).</param>
/// <param name="OldName">The previous name of the view, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the view.</param>
/// <param name="DependsOn">The objects the view reads, derived from <paramref name="Body"/>.</param>
[DebuggerDisplay("{Name,nq} (view)")]
public sealed record View(
    string Name,
    string Body,
    string? OldName = null,
    string? Comment = null,
    IReadOnlyList<ViewDependency>? DependsOn = null
)
{
    /// <summary>
    /// The objects the view reads, derived from <see cref="Body"/>.
    /// </summary>
    public IReadOnlyList<ViewDependency> DependsOn { get; init; } = DependsOn ?? [];
}
