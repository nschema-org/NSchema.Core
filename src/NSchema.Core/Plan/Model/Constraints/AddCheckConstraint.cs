using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Model.Constraints;

/// <summary>
/// Represents adding a new check constraint to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the check constraint will be added.</param>
/// <param name="TableName">The name of the table to which the check constraint will be added.</param>
/// <param name="CheckConstraint">The definition of the check constraint to be added.</param>
public sealed record AddCheckConstraint(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    CheckConstraint CheckConstraint
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
