namespace NSchema.Plan.Domain.Models.Constraints;

/// <summary>
/// Represents the removal of an existing check constraint from a table in the database schema. Dropping a check
/// only loosens validation (no data is lost), so it is not treated as a destructive action.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the check constraint will be removed.</param>
/// <param name="TableName">The name of the table from which the check constraint will be removed.</param>
/// <param name="ConstraintName">The name of the check constraint to be removed.</param>
public sealed record DropCheckConstraint(
    string SchemaName,
    string TableName,
    string ConstraintName
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
