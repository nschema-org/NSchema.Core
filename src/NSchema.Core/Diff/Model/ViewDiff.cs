using NSchema.Schema.Model.Views;

namespace NSchema.Diff.Model;

/// <summary>
/// Describes a change to a view.
/// </summary>
/// <param name="Schema">The name of the schema the view belongs to.</param>
/// <param name="Name">The view name.</param>
/// <param name="Kind">The change to the view.</param>
/// <param name="RenamedFrom">The previous view name when the view is being renamed; otherwise <see langword="null"/>.</param>
/// <param name="Definition">The view definition for an added or body-modified view; otherwise <see langword="null"/>.</param>
/// <param name="Comment">The change to the view's comment, if any.</param>
/// <param name="DependsOn">
/// The objects the view reads, used to order it relative to other views in the plan. Populated from the desired
/// view for an add/modify and from the current view for a removal.
/// </param>
public sealed record ViewDiff(
    string Schema,
    string Name,
    ChangeKind Kind,
    string? RenamedFrom = null,
    View? Definition = null,
    ValueChange<string>? Comment = null,
    IReadOnlyList<ViewDependency>? DependsOn = null
) : ISchemaObjectDiff
{
    /// <summary>
    /// The objects the view reads, used to order it relative to other views in the plan.
    /// </summary>
    public IReadOnlyList<ViewDependency> DependsOn { get; init; } = DependsOn ?? [];
}
