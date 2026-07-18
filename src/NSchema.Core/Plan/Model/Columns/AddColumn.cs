using NSchema.Model;
using NSchema.Model.Columns;

namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents adding a new column to an existing table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table to which the column will be added.</param>
/// <param name="TableName">The name of the table to which the column will be added.</param>
/// <param name="Column">The definition of the column to be added.</param>
public sealed record AddColumn(SqlIdentifier SchemaName, SqlIdentifier TableName, Column Column) : MigrationAction;
