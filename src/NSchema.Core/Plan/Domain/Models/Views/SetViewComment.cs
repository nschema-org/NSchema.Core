using NSchema.Project.Domain.Models;
namespace NSchema.Plan.Domain.Models.Views;

/// <summary>
/// Represents setting, changing, or clearing the comment on a view.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view.</param>
/// <param name="ViewName">The name of the view.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
/// <param name="IsMaterialized">Whether the commented view is a materialized view.</param>
public sealed record SetViewComment(SqlIdentifier SchemaName, SqlIdentifier ViewName, string? OldComment, string? NewComment, bool IsMaterialized = false) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
