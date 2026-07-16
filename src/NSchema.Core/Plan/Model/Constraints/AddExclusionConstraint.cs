using NSchema.Model;
using NSchema.Model.Constraints;

namespace NSchema.Plan.Model.Constraints;

/// <summary>
/// Represents adding a new exclusion constraint to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the exclusion constraint will be added.</param>
/// <param name="TableName">The name of the table to which the exclusion constraint will be added.</param>
/// <param name="ExclusionConstraint">The definition of the exclusion constraint to be added.</param>
public sealed record AddExclusionConstraint(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    ExclusionConstraint ExclusionConstraint
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
