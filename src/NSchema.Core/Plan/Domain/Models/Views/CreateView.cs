using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Plan.Domain.Models.Views;

/// <summary>
/// Represents the creation (or in-place replacement) of a view.
/// </summary>
/// <param name="SchemaName">The name of the schema the view belongs to.</param>
/// <param name="View">The definition of the view to create or replace.</param>
public sealed record CreateView(SqlIdentifier SchemaName, View View) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
