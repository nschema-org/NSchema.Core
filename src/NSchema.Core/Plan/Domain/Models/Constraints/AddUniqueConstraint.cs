using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Constraints;

namespace NSchema.Plan.Domain.Models.Constraints;

/// <summary>
/// Represents adding a new unique constraint to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the unique constraint will be added.</param>
/// <param name="TableName">The name of the table to which the unique constraint will be added.</param>
/// <param name="UniqueConstraint">The definition of the unique constraint to be added.</param>
public sealed record AddUniqueConstraint(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    UniqueConstraint UniqueConstraint
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
