namespace NSchema.Plan.Model.Functions;

/// <summary>
/// Represents setting, changing, or clearing the comment on a function.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the function.</param>
/// <param name="FunctionName">The name of the function.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetFunctionComment(string SchemaName, string FunctionName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
