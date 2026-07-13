using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Diff.Domain.Models.Views;

/// <summary>
/// Describes a change to a view (plain or materialized).
/// </summary>
/// <param name="Schema">The name of the schema the view belongs to.</param>
/// <param name="Name">The view name.</param>
/// <param name="Kind">The change to the view.</param>
/// <param name="RenamedFrom">The previous view name when the view is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The view definition for an added or body-modified view; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the view's comment, if any.</param>
/// <param name="DependsOn">The objects the view reads, used to order it relative to other views in the plan.</param>
/// <param name="IsMaterialized">Whether the view is materialized (after the change, for a modified view).</param>
/// <param name="Materialized">The change to the view's materialization when it converts between a plain and a materialized view; otherwise <see langword="null"/>.</param>
/// <param name="RequiresRecreate">Whether the change must be applied as a drop + recreate rather than an in-place replace</param>
/// <param name="Indexes">In-place index changes on a materialized view whose body is unchanged.</param>
public sealed record ViewDiff(
    SqlIdentifier Schema,
    SqlIdentifier Name,
    ChangeKind Kind,
    SqlIdentifier? RenamedFrom = null,
    View? Definition = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<ViewDependency>? DependsOn = null,
    bool IsMaterialized = false,
    ValueChange<bool>? Materialized = null,
    bool RequiresRecreate = false,
    IReadOnlyList<IndexDiff>? Indexes = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// The objects the view reads, used to order it relative to other views in the plan.
    /// </summary>
    public IReadOnlyList<ViewDependency> DependsOn { get; init; } = DependsOn ?? [];

    /// <summary>
    /// In-place index changes on a materialized view whose body is unchanged.
    /// </summary>
    public IReadOnlyList<IndexDiff> Indexes { get; init; } = Indexes ?? [];
}
