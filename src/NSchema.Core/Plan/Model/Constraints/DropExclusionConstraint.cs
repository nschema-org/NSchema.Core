namespace NSchema.Plan.Model.Constraints;

/// <summary>
/// Represents the removal of an existing exclusion constraint from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the exclusion constraint will be removed.</param>
/// <param name="TableName">The name of the table from which the exclusion constraint will be removed.</param>
/// <param name="ConstraintName">The name of the exclusion constraint to be removed.</param>
public sealed record DropExclusionConstraint(
    string SchemaName,
    string TableName,
    string ConstraintName
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
