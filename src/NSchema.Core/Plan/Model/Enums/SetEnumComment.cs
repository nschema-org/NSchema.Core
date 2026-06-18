namespace NSchema.Plan.Model.Enums;

/// <summary>
/// Represents setting, changing, or clearing the comment on an enum type.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the enum.</param>
/// <param name="EnumName">The name of the enum.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetEnumComment(string SchemaName, string EnumName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
