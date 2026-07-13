using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;

namespace NSchema.Plan.Domain.Models.Columns;

/// <summary>
/// Represents the removal of an existing column from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the column will be removed.</param>
/// <param name="TableName">The name of the table from which the column will be removed.</param>
/// <param name="Column">The column to be removed, including its type and nullability.</param>
public sealed record DropColumn(SqlIdentifier SchemaName, SqlIdentifier TableName, Column Column) : MigrationAction
{
    /// <summary>The name of the column to be removed.</summary>
    public SqlIdentifier ColumnName => Column.Name;

    /// <inheritdoc />
    public override bool IsDestructive => true;
}
