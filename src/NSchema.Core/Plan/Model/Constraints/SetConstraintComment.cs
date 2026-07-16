using NSchema.Model;
namespace NSchema.Plan.Domain.Models.Constraints;

/// <summary>
/// Represents setting or clearing the comment on an existing constraint.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table whose constraint comment will be modified.</param>
/// <param name="TableName">The name of the table whose constraint comment will be modified.</param>
/// <param name="ConstraintName">The name of the constraint whose comment will be modified.</param>
/// <param name="OldComment">The current comment before modification, or null if there is none.</param>
/// <param name="NewComment">The new comment to set, or null if the comment is being removed.</param>
public sealed record SetConstraintComment(SqlIdentifier SchemaName, SqlIdentifier TableName, SqlIdentifier ConstraintName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
