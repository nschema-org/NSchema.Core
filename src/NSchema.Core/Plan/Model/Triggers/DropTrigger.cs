namespace NSchema.Plan.Model.Triggers;

/// <summary>
/// Represents the removal of an existing trigger from a table.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table the trigger is attached to.</param>
/// <param name="TableName">The name of the table the trigger is attached to.</param>
/// <param name="TriggerName">The name of the trigger to remove.</param>
public sealed record DropTrigger(string SchemaName, string TableName, string TriggerName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
