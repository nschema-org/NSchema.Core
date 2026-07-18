using NSchema.Model;

namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents the renaming of an existing column in a table in the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table in which the column will be renamed.</param>
/// <param name="TableName">The name of the table in which the column will be renamed.</param>
/// <param name="OldName">The current name of the column to be renamed.</param>
/// <param name="NewName">The new name for the column to be renamed.</param>
public sealed record RenameColumn(
    SqlIdentifier SchemaName,
    SqlIdentifier TableName,
    SqlIdentifier OldName,
    SqlIdentifier NewName
) : MigrationAction;
