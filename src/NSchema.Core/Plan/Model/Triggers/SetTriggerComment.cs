using NSchema.Model;

namespace NSchema.Plan.Model.Triggers;

/// <summary>
/// Represents setting, changing, or clearing the comment on a trigger.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table the trigger is attached to.</param>
/// <param name="TableName">The name of the table the trigger is attached to.</param>
/// <param name="TriggerName">The name of the trigger.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetTriggerComment(SqlIdentifier SchemaName, SqlIdentifier TableName, SqlIdentifier TriggerName, string? OldComment, string? NewComment) : MigrationAction;
