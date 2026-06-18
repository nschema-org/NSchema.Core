namespace NSchema.Plan.Model.Views;

/// <summary>
/// Represents setting, changing, or clearing the comment on a view.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view.</param>
/// <param name="ViewName">The name of the view.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetViewComment(string SchemaName, string ViewName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
