using NSchema.Model;
using NSchema.Model.Triggers;

namespace NSchema.Plan.Domain.Models.Triggers;

/// <summary>
/// Represents adding a new trigger to an existing table.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table the trigger is attached to.</param>
/// <param name="TableName">The name of the table the trigger is attached to.</param>
/// <param name="Trigger">The definition of the trigger to add.</param>
public sealed record CreateTrigger(SqlIdentifier SchemaName, SqlIdentifier TableName, Trigger Trigger) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
