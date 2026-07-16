using NSchema.Model;
using NSchema.Model.Tables;

namespace NSchema.Plan.Model.Tables;

/// <summary>
/// Represents adding a new foreign key constraint to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the foreign key will be added.</param>
/// <param name="TableName">The name of the table to which the foreign key will be added.</param>
/// <param name="ForeignKey">The definition of the foreign key constraint to be added.</param>
public sealed record AddForeignKey(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    ForeignKey ForeignKey
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
