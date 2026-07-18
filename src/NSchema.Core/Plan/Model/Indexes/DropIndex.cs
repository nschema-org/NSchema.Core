using NSchema.Model;

namespace NSchema.Plan.Model.Indexes;

/// <summary>
/// Represents the removal of an existing index from a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table from which the index will be removed.</param>
/// <param name="TableName">The name of the table from which the index will be removed.</param>
/// <param name="IndexName">The name of the index to be removed.</param>
public sealed record DropIndex(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    SqlIdentifier IndexName
) : MigrationAction;
