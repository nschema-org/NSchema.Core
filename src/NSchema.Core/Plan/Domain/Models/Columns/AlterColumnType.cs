using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Plan.Domain.Models.Columns;

/// <summary>
/// Represents changing the data type of an existing column in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table with the column to be altered.</param>
/// <param name="TableName">The name of the table containing the column to be altered.</param>
/// <param name="ColumnName">The name of the column whose data type is to be altered.</param>
/// <param name="OldType">The current data type of the column before alteration.</param>
/// <param name="NewType">The new data type of the column after alteration.</param>
/// <param name="IsNullable">The column's nullability in its final (desired) state.</param>
/// <remarks>
/// This action may involve data transformation and can potentially lead to data loss if the new type is not compatible with the existing data.
/// Therefore, it is considered a destructive migration action.
/// </remarks>
public sealed record AlterColumnType(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    SqlIdentifier ColumnName,
    SqlType OldType,
    SqlType NewType,
    bool? IsNullable = null
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
